/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AuthenticatorTransport } from './AuthenticatorTransport';
import type { PublicKeyCredentialType } from './PublicKeyCredentialType';
export type PublicKeyCredentialDescriptor = {
    type?: PublicKeyCredentialType;
    id?: string | null;
    transports?: Array<AuthenticatorTransport> | null;
};

