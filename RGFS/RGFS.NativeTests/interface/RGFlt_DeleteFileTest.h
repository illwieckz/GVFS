#pragma once

extern "C"
{
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteVirtualFile_SetDisposition(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteVirtualFile_DeleteOnClose(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeletePlaceholder_SetDisposition(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeletePlaceholder_DeleteOnClose(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteFullFile_SetDisposition(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteFullFile_DeleteOnClose(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteLocalFile_SetDisposition(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteLocalFile_DeleteOnClose(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteNotExistFile_SetDisposition(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteNotExistFile_DeleteOnClose(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteNonRootVirtualFile_SetDisposition(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteNonRootVirtualFile_DeleteOnClose(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteFileOutsideVRoot_SetDisposition(const char* pathOutsideRepo);
	NATIVE_TESTS_EXPORT bool RGFlt_DeleteFileOutsideVRoot_DeleteOnClose(const char* pathOutsideRepo);

	// Note the following tests were not ported from RGFlt:
	//
	// DeleteFullFileWithoutFileContext_SetDisposition
	// DeleteFullFileWithoutFileContext_DeleteOnClose
	//    - RGFS will always project new files when its back layer changes 
}
