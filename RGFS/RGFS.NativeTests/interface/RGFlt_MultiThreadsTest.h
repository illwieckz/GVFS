#pragma once

extern "C"
{
	NATIVE_TESTS_EXPORT bool RGFlt_OpenForReadsSameTime(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_OpenForWritesSameTime(const char* virtualRootPath);

	// Note the following tests were not ported from RGFlt:
	//
	// GetPlaceholderInfoAndStopInstance
	// GetStreamAndStopInstance
	// EnumAndStopInstance
	//    - These tests require precise control of when the virtualization instance is stopped

    // Note: RGFlt_OpenMultipleFilesForReadsSameTime was not ported from RgFlt code, it just follows
    // the same pattern as those tests
    NATIVE_TESTS_EXPORT bool RGFlt_OpenMultipleFilesForReadsSameTime(const char* virtualRootPath);
}
