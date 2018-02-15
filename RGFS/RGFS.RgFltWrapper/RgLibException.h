#pragma once

#include "NtStatus.h"

namespace RgLib
{
    [System::Serializable]
    public ref class RgLibException : System::Exception
    {
    public:
        RgLibException(System::String^ errorMessage);
        RgLibException(NtStatus errorCode);
        RgLibException(System::String^ errorMessage, NtStatus errorCode);

        virtual System::String^ ToString() override;

        [System::Security::Permissions::SecurityPermission(
            System::Security::Permissions::SecurityAction::LinkDemand, 
            Flags = System::Security::Permissions::SecurityPermissionFlag::SerializationFormatter)]
        virtual void GetObjectData(
            System::Runtime::Serialization::SerializationInfo^ info,
            System::Runtime::Serialization::StreamingContext context) override;

        virtual property NtStatus ErrorCode
        {
            NtStatus get(void);
        };

    protected:
        RgLibException(
            System::Runtime::Serialization::SerializationInfo^ info, 
            System::Runtime::Serialization::StreamingContext context);

    private:
        NtStatus errorCode;
    };
}