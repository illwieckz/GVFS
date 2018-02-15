#include "stdafx.h"
#include "PlaceholderUtils.h"
#include "SafeHandle.h"
#include "TestException.h"
#include "TestHelpers.h"
#include "TestVerifiers.h"
#include "Should.h"

using namespace TestHelpers;

bool PlaceHolderHasVersionInfo(const char* virtualPath, int version, const WCHAR* sha)
{
    try
    {
        std::string path(virtualPath);
        std::shared_ptr<GV_REPARSE_INFO> reparseInfo = GetReparseInfo(path);

        SHOULD_EQUAL(reparseInfo->versionInfo.EpochID[0], static_cast<UCHAR>(version));

        SHOULD_EQUAL(_wcsnicmp(sha, static_cast<WCHAR*>(static_cast<void*>(reparseInfo->versionInfo.ContentID)), RGFLT_PLACEHOLDER_ID_LENGTH), 0);
    }
    catch (TestException&)
    {
        return false;
    }

    return true;

}
