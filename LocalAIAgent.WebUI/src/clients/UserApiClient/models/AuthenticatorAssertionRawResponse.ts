/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AssertionResponse } from './AssertionResponse';
import type { AuthenticationExtensionsClientOutputs } from './AuthenticationExtensionsClientOutputs';
import type { PublicKeyCredentialType } from './PublicKeyCredentialType';
export type AuthenticatorAssertionRawResponse = {
    id: string;
    rawId: string;
    response?: AssertionResponse;
    type: PublicKeyCredentialType;
    extensions?: AuthenticationExtensionsClientOutputs;
    clientExtensionResults: AuthenticationExtensionsClientOutputs;
};

