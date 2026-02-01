/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AttestationResponse } from './AttestationResponse';
import type { AuthenticationExtensionsClientOutputs } from './AuthenticationExtensionsClientOutputs';
import type { PublicKeyCredentialType } from './PublicKeyCredentialType';
export type AuthenticatorAttestationRawResponse = {
    id: string;
    rawId: string;
    type: PublicKeyCredentialType;
    response: AttestationResponse;
    extensions?: AuthenticationExtensionsClientOutputs;
    clientExtensionResults: AuthenticationExtensionsClientOutputs;
};

