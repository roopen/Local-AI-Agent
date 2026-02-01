/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AuthenticatorTransport } from './AuthenticatorTransport';
import type { Fido2User } from './Fido2User';
import type { PublicKeyCredentialType } from './PublicKeyCredentialType';
export type RegisteredPublicKeyCredential = {
    type?: PublicKeyCredentialType;
    id?: string | null;
    publicKey?: string | null;
    transports?: Array<AuthenticatorTransport> | null;
    signCount?: number;
    isBackupEligible?: boolean;
    isBackedUp?: boolean;
    aaGuid?: string;
    user?: Fido2User;
    attestationFormat?: string | null;
    attestationObject?: string | null;
    attestationClientDataJson?: string | null;
};

