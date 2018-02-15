#include "stdafx.h"
#include "RgLibException.h"

using namespace System;
using namespace System::Globalization;
using namespace RgLib;

RgLibException::RgLibException(String^ errorMessage)
    : RgLibException(errorMessage, NtStatus::InternalError)
{
}

RgLibException::RgLibException(NtStatus errorCode)
	: RgLibException("RgLibException exception, error: " + errorCode.ToString(), errorCode)
{
}
 
RgLibException::RgLibException(String^ errorMessage, NtStatus errorCode)
    : Exception(errorMessage)
    , errorCode(errorCode)
{
}

RgLibException::RgLibException(
    System::Runtime::Serialization::SerializationInfo^ info,
    System::Runtime::Serialization::StreamingContext context)
    : Exception(info, context)
{
}

String^ RgLibException::ToString()
{
    return String::Format(CultureInfo::InvariantCulture, "RgLibException ErrorCode: {0}, {1}", + this->errorCode, this->Exception::ToString());
}

void RgLibException::GetObjectData(
    System::Runtime::Serialization::SerializationInfo^ info,
    System::Runtime::Serialization::StreamingContext context)
{
    Exception::GetObjectData(info, context);
}

NtStatus RgLibException::ErrorCode::get(void)
{
    return this->errorCode;
};