#pragma once

extern "C"
{
	NATIVE_TESTS_EXPORT bool RGFlt_ModifyFileInScratchAndDir(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_RMDIRTest1(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_RMDIRTest2(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_RMDIRTest3(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_RMDIRTest4(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_RMDIRTest5(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_DeepNonExistFileUnderPartial(const char* virtualRootPath);
	NATIVE_TESTS_EXPORT bool RGFlt_SupersededReparsePoint(const char* virtualRootPath);

	// Note the following tests were not ported from RGFlt:
	//
	// StartInstanceAndFreeCallbacks
	// QickAttachDetach
	//   - These timing scenarios don't need to be tested with RGFS
	//
	// UnableToReadPartialFile
	//   - This test requires control over the RGFlt callback implementation
	//
	// DeepNonExistFileUnderFull
	//   - Currently RGFS does not covert folders to full

	// The following were ported to the managed tests:
	//
	// CMDHangNoneActiveInstance
}
