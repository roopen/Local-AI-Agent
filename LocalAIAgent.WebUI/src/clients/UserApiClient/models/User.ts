/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { Fido2Credential } from './Fido2Credential';
import type { UserPreferences } from './UserPreferences';
export type User = {
    id?: number;
    fido2Id: string | null;
    username?: string | null;
    passwordHash?: string | null;
    preferences?: UserPreferences;
    fido2Credentials?: Array<Fido2Credential> | null;
};

