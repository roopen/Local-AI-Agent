/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AuthenticationExtensionsLargeBlobInputs } from './AuthenticationExtensionsLargeBlobInputs';
import type { AuthenticationExtensionsPRFInputs } from './AuthenticationExtensionsPRFInputs';
import type { CredentialProtectionPolicy } from './CredentialProtectionPolicy';
export type AuthenticationExtensionsClientInputs = {
    'example.extension.bool'?: boolean | null;
    exts?: boolean | null;
    uvm?: boolean | null;
    credProps?: boolean | null;
    prf?: AuthenticationExtensionsPRFInputs;
    largeBlob?: AuthenticationExtensionsLargeBlobInputs;
    credentialProtectionPolicy?: CredentialProtectionPolicy;
    enforceCredentialProtectionPolicy?: boolean | null;
};

