//go:build !windows

package main

func listAdapters() []gpuInfo { return nil }
