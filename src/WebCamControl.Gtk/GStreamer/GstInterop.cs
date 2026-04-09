// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2025 Daniel Lo Nigro <d@d.sb>

using System.Runtime.InteropServices;

namespace WebCamControl.Gtk.GStreamer;

/// <summary>
/// P/Invoke bindings for GStreamer.
/// </summary>
internal static partial class GstInterop
{
	private const string LibGst = "libgstreamer-1.0.so.0";
	private const string LibGstApp = "libgstapp-1.0.so.0";

	// ── Lifecycle ──────────────────────────────────────────────────────────

	[LibraryImport(LibGst, EntryPoint = "gst_init")]
	public static partial void Init(ref int argc, IntPtr argv);

	[LibraryImport(LibGst, EntryPoint = "gst_object_unref")]
	public static partial void ObjectUnref(IntPtr obj);

	[LibraryImport(LibGst, EntryPoint = "gst_mini_object_unref")]
	public static partial void MiniObjectUnref(IntPtr obj);

	// ── Pipeline ──────────────────────────────────────────────────────────

	[LibraryImport(LibGst, EntryPoint = "gst_parse_launch", StringMarshalling = StringMarshalling.Utf8)]
	public static partial IntPtr ParseLaunch(string pipelineDescription, IntPtr error);

	[LibraryImport(LibGst, EntryPoint = "gst_element_set_state")]
	public static partial int ElementSetState(IntPtr element, GstState state);

	[LibraryImport(LibGst, EntryPoint = "gst_bin_get_by_name", StringMarshalling = StringMarshalling.Utf8)]
	public static partial IntPtr BinGetByName(IntPtr bin, string name);

	// ── AppSink ───────────────────────────────────────────────────────────

	/// <summary>
	/// Pull a sample from the appsink. Returns zero if EOS or timeout.
	/// timeout is in nanoseconds; use 0 for non-blocking.
	/// </summary>
	[LibraryImport(LibGstApp, EntryPoint = "gst_app_sink_try_pull_sample")]
	public static partial IntPtr AppSinkTryPullSample(IntPtr appSink, ulong timeout);

	// ── Sample / Buffer ───────────────────────────────────────────────────

	[LibraryImport(LibGst, EntryPoint = "gst_sample_get_buffer")]
	public static partial IntPtr SampleGetBuffer(IntPtr sample);

	[LibraryImport(LibGst, EntryPoint = "gst_sample_get_caps")]
	public static partial IntPtr SampleGetCaps(IntPtr sample);

	[LibraryImport(LibGst, EntryPoint = "gst_sample_unref")]
	public static partial void SampleUnref(IntPtr sample);

	[LibraryImport(LibGst, EntryPoint = "gst_buffer_map")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool BufferMap(IntPtr buffer, out GstMapInfo info, GstMapFlags flags);

	[LibraryImport(LibGst, EntryPoint = "gst_buffer_unmap")]
	public static partial void BufferUnmap(IntPtr buffer, ref GstMapInfo info);

	// ── Caps / Structure ──────────────────────────────────────────────────

	[LibraryImport(LibGst, EntryPoint = "gst_caps_get_structure")]
	public static partial IntPtr CapsGetStructure(IntPtr caps, uint index);

	[LibraryImport(LibGst, EntryPoint = "gst_structure_get_int", StringMarshalling = StringMarshalling.Utf8)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool StructureGetInt(IntPtr structure, string fieldName, out int value);
}

internal enum GstState
{
	VoidPending = 0,
	Null = 1,
	Ready = 2,
	Paused = 3,
	Playing = 4,
}

internal enum GstMapFlags
{
	Read = 1,
}

/// <summary>
/// Mirrors the GstMapInfo native struct.
/// https://gstreamer.freedesktop.org/documentation/gstreamer/gstmemory.html#GstMapInfo
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GstMapInfo
{
	public IntPtr Memory;
	public int Flags;
	public IntPtr Data;
	public nuint Size;
	public nuint MaxSize;
	public IntPtr UserData0;
	public IntPtr UserData1;
	public IntPtr UserData2;
	public IntPtr UserData3;
	public IntPtr Reserved0;
	public IntPtr Reserved1;
	public IntPtr Reserved2;
	public IntPtr Reserved3;
}