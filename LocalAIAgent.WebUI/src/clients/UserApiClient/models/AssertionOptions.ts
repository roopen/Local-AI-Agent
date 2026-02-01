/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AuthenticationExtensionsClientInputs } from './AuthenticationExtensionsClientInputs';
import type { PublicKeyCredentialDescriptor } from './PublicKeyCredentialDescriptor';
import type { PublicKeyCredentialHint } from './PublicKeyCredentialHint';
import type { UserVerificationRequirement } from './UserVerificationRequirement';
export type AssertionOptions = {
    challenge?: string | null;
    timeout?: number;
    rpId?: string | null;
    allowCredentials?: Array<PublicKeyCredentialDescriptor> | null;
    userVerification?: UserVerificationRequirement;
    hints?: Array<PublicKeyCredentialHint> | null;
    extensions?: AuthenticationExtensionsClientInputs;
};

