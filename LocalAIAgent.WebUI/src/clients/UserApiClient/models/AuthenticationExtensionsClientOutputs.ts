/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AuthenticationExtensionsLargeBlobOutputs } from './AuthenticationExtensionsLargeBlobOutputs';
import type { AuthenticationExtensionsPRFOutputs } from './AuthenticationExtensionsPRFOutputs';
import type { CredentialPropertiesOutput } from './CredentialPropertiesOutput';
import type { CredentialProtectionPolicy } from './CredentialProtectionPolicy';
export type AuthenticationExtensionsClientOutputs = {
    'example.extension.bool'?: boolean | null;
    appid?: boolean;
    exts?: Array<string> | null;
    uvm?: Array<Array<number>> | null;
    credProps?: CredentialPropertiesOutput;
    prf?: AuthenticationExtensionsPRFOutputs;
    largeBlob?: AuthenticationExtensionsLargeBlobOutputs;
    credProtect?: CredentialProtectionPolicy;
};

