/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AuthenticatorAttachment } from './AuthenticatorAttachment';
import type { ResidentKeyRequirement } from './ResidentKeyRequirement';
import type { UserVerificationRequirement } from './UserVerificationRequirement';
export type AuthenticatorSelection = {
    authenticatorAttachment?: AuthenticatorAttachment;
    residentKey?: ResidentKeyRequirement;
    /**
     * @deprecated
     */
    requireResidentKey?: boolean;
    userVerification?: UserVerificationRequirement;
};

