//go:build windows

package main

import (
	"fmt"
	"syscall"
	"unsafe"
)

type dxgiLUID struct {
	Low  uint32
	High int32
}

type dxgiAdapterDesc1 struct {
	Description           [128]uint16
	VendorID              uint32
	DeviceID              uint32
	SubSysID              uint32
	Revision              uint32
	DedicatedVideoMemory  uintptr
	DedicatedSystemMemory uintptr
	SharedSystemMemory    uintptr
	AdapterLuid           dxgiLUID
	Flags                 uint32
}

var iidDXGIFactory1 = syscall.GUID{Data1: 0x770aae78, Data2: 0xf26f, Data3: 0x4dba, Data4: [8]byte{0xa8, 0x29, 0x25, 0x3c, 0x83, 0xd1, 0xb3, 0x87}}

// call method idx of a COM object's vtable
func comCall(obj uintptr, idx int, args ...uintptr) uintptr {
	vtbl := *(*uintptr)(unsafe.Pointer(obj))
	fn := *(*uintptr)(unsafe.Pointer(vtbl + uintptr(idx)*unsafe.Sizeof(uintptr(0))))
	r, _, _ := syscall.SyscallN(fn, append([]uintptr{obj}, args...)...)
	return r
}

// listAdapters asks DirectX for every hardware graphics card in the machine
func listAdapters() (out []gpuInfo) {
	defer func() {
		if recover() != nil {
			out = nil
		}
	}()
	dll, err := syscall.LoadDLL("dxgi.dll")
	if err != nil {
		return nil
	}
	proc, err := dll.FindProc("CreateDXGIFactory1")
	if err != nil {
		return nil
	}
	var factory uintptr
	r, _, _ := proc.Call(uintptr(unsafe.Pointer(&iidDXGIFactory1)), uintptr(unsafe.Pointer(&factory)))
	if r != 0 || factory == 0 {
		return nil
	}
	defer comCall(factory, 2) // Release
	for i := 0; i < 16; i++ {
		var adapter uintptr
		if hr := comCall(factory, 12, uintptr(i), uintptr(unsafe.Pointer(&adapter))); hr != 0 || adapter == 0 { // EnumAdapters1
			break
		}
		var d dxgiAdapterDesc1
		hr := comCall(adapter, 10, uintptr(unsafe.Pointer(&d))) // GetDesc1
		comCall(adapter, 2)
		if hr != 0 || d.Flags&2 != 0 { // skip the software adapter
			continue
		}
		out = append(out, gpuInfo{
			Name: syscall.UTF16ToString(d.Description[:]),
			Luid: fmt.Sprintf("%d,%d", d.AdapterLuid.High, d.AdapterLuid.Low),
			VRAM: uint64(d.DedicatedVideoMemory) / (1024 * 1024),
		})
	}
	return out
}
