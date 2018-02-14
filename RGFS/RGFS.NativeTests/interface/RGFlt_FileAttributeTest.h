#pragma once

extern "C"
{
	NATIVE_TESTS_EXPORT bool RGFlt_ModifyFileInScratchAndCheckLastWriteTime(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_FileSize(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_ModifyFileInScratchAndCheckFileSize(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_FileAttributes(const char* virtualRootPath);

	// Note the following tests were not ported from RGFlt:
	//
	// LastWriteTime
	//     - There is no last write time in the RGFS layer to compare with
}
