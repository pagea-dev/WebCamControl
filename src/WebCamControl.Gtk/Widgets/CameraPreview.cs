// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2025 Daniel Lo Nigro <d@d.sb>

using Gdk;
using Gtk;
using WebCamControl.Core;
using WebCamControl.Gtk.GStreamer;

namespace WebCamControl.Gtk.Widgets;

/// <summary>
/// A widget that shows a live preview from a V4L2 camera using GStreamer.
/// Frames are pulled on the GLib main loop via a timeout source.
/// </summary>
public sealed class CameraPreview : Box, IDisposable
{
	private static bool _gstInitialized;

	private readonly Picture _picture;
	private IntPtr _pipeline;
	private IntPtr _appSink;
	private bool _disposed;

	// Keep a strong reference so the delegate isn't GC'd while the timeout is active.
	private readonly GLib.SourceFunc _pollDelegate;

	/// <param name="camera">Camera to preview.</param>
	/// <param name="width">Requested display width in pixels.</param>
	/// <param name="height">Requested display height in pixels.</param>
	public CameraPreview(ICamera camera, int width, int height)
	{
		_picture = Picture.New();
		_picture.ContentFit = ContentFit.Contain;
		_picture.WidthRequest = width;
		_picture.HeightRequest = height;
		Append(_picture);

		EnsureGstInitialized();
		StartPipeline(camera.RawName);

		_pollDelegate = PollFrame;
		// Poll for a new frame roughly 30 times per second.
		GLib.Functions.TimeoutAdd(0, 33, _pollDelegate);
	}

	// ── GStreamer lifecycle ──────────────────────────────────────────────

	private static void EnsureGstInitialized()
	{
		if (_gstInitialized) return;
		var argc = 0;
		GstInterop.Init(ref argc, IntPtr.Zero);
		_gstInitialized = true;
	}

	private void StartPipeline(string rawDeviceName)
	{
		var devicePath = $"/dev/{rawDeviceName}";
		// Request raw RGB output so we can wrap it directly into a Gdk.MemoryTexture.
		var description =
			$"v4l2src device={devicePath} " +
			"! videoconvert " +
			"! video/x-raw,format=RGB " +
			"! appsink name=sink sync=false max-buffers=1 drop=true";

		_pipeline = GstInterop.ParseLaunch(description, IntPtr.Zero);
		if (_pipeline == IntPtr.Zero)
		{
			throw new InvalidOperationException($"Failed to create GStreamer pipeline for {devicePath}");
		}

		_appSink = GstInterop.BinGetByName(_pipeline, "sink");
		if (_appSink == IntPtr.Zero)
		{
			GstInterop.ObjectUnref(_pipeline);
			_pipeline = IntPtr.Zero;
			throw new InvalidOperationException("Failed to find appsink element in pipeline");
		}

		GstInterop.ElementSetState(_pipeline, GstState.Playing);
	}

	// ── Frame polling (runs on the GLib main loop) ───────────────────────

	private bool PollFrame()
	{
		if (_disposed || _appSink == IntPtr.Zero)
			return false; // remove timeout source

		// Non-blocking: timeout = 0 nanoseconds.
		var sample = GstInterop.AppSinkTryPullSample(_appSink, timeout: 0);
		if (sample == IntPtr.Zero)
			return !_disposed; // no frame yet, keep waiting

		try
		{
			var texture = BuildTexture(sample);
			if (texture != null)
			{
				_picture.SetPaintable(texture);
			}
		}
		finally
		{
			GstInterop.SampleUnref(sample);
		}

		return !_disposed;
	}

	private static MemoryTexture? BuildTexture(IntPtr sample)
	{
		var caps = GstInterop.SampleGetCaps(sample);
		if (caps == IntPtr.Zero) return null;

		var structure = GstInterop.CapsGetStructure(caps, 0);
		if (structure == IntPtr.Zero) return null;

		if (!GstInterop.StructureGetInt(structure, "width", out var width) ||
		    !GstInterop.StructureGetInt(structure, "height", out var height) ||
		    width <= 0 || height <= 0)
		{
			return null;
		}

		var buffer = GstInterop.SampleGetBuffer(sample);
		if (buffer == IntPtr.Zero) return null;

		if (!GstInterop.BufferMap(buffer, out var mapInfo, GstMapFlags.Read))
			return null;

		try
		{
			// Copy the raw RGB bytes into a managed array so the GstBuffer can be
			// released before GTK processes the texture.
			var byteCount = (int)mapInfo.Size;
			var bytes = new byte[byteCount];
			System.Runtime.InteropServices.Marshal.Copy(mapInfo.Data, bytes, 0, byteCount);

			var gbytes = GLib.Bytes.New(bytes.AsSpan());
			return MemoryTexture.New(width, height, MemoryFormat.R8g8b8, gbytes, (nuint)(width * 3));
		}
		finally
		{
			GstInterop.BufferUnmap(buffer, ref mapInfo);
		}
	}

	// ── IDisposable ──────────────────────────────────────────────────────

	public new void Dispose()
	{
		if (_disposed) return;
		_disposed = true;

		if (_pipeline != IntPtr.Zero)
		{
			GstInterop.ElementSetState(_pipeline, GstState.Null);
		}

		if (_appSink != IntPtr.Zero)
		{
			GstInterop.ObjectUnref(_appSink);
			_appSink = IntPtr.Zero;
		}

		if (_pipeline != IntPtr.Zero)
		{
			GstInterop.ObjectUnref(_pipeline);
			_pipeline = IntPtr.Zero;
		}

		base.Dispose();
	}
}