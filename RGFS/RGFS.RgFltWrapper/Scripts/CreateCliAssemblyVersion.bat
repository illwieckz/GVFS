mkdir %2\BuildOutput
mkdir %2\BuildOutput\RgLib.Managed
echo #include "stdafx.h" > %2\BuildOutput\RgLib.Managed\AssemblyVersion.h
echo using namespace System::Reflection; [assembly:AssemblyVersion("%1")];[assembly:AssemblyFileVersion("%1")]; >> %2\BuildOutput\RgLib.Managed\AssemblyVersion.h