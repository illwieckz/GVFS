#pragma once

extern "C"
{
	NATIVE_TESTS_EXPORT bool RGFlt_EnumEmptyFolder(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_EnumFolderWithOneFileInPackage(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_EnumFolderWithOneFileInBoth(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_EnumFolderWithOneFileInBoth1(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_EnumFolderDeleteExistingFile(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_EnumFolderSmallBuffer(const char* virtualRootPath);
}
