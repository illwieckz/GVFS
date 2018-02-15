mkdir %2\BuildOutput
mkdir %2\BuildOutput\RgLib.Managed

set comma_version_string=%1
set comma_version_string=%comma_version_string:.=,%

echo #define RGLIB_FILE_VERSION %comma_version_string% > %2\BuildOutput\RgLib.Managed\VersionHeader.h
echo #define RGLIB_FILE_VERSION_STRING "%1" >> %2\BuildOutput\RgLib.Managed\VersionHeader.h
echo #define RGLIB_PRODUCT_VERSION %comma_version_string% >> %2\BuildOutput\RgLib.Managed\VersionHeader.h
echo #define RGLIB_PRODUCT_VERSION_STRING "%1" >> %2\BuildOutput\RgLib.Managed\VersionHeader.h