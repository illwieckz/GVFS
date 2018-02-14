#pragma once

extern "C"
{
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteVirtualNonEmptyFolder_SetDisposition(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteVirtualNonEmptyFolder_DeleteOnClose(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeletePlaceholderNonEmptyFolder_SetDisposition(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeletePlaceholderNonEmptyFolder_DeleteOnClose(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteLocalEmptyFolder_SetDisposition(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteLocalEmptyFolder_DeleteOnClose(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteNonRootVirtualFolder_SetDisposition(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteNonRootVirtualFolder_DeleteOnClose(const char* virtualRootPath);

	// Note the following tests were not ported from RGFlt:
	//
	// DeleteVirtualEmptyFolder_SetDisposition
	// DeleteVirtualEmptyFolder_DeleteOnClose
	//    - Git does not support empty folders
	//
	// DeleteFullNonEmptyFolder_SetDisposition
	// DeleteFullNonEmptyFolder_DeleteOnClose
	//    - RGFS does not allow full folders
}
