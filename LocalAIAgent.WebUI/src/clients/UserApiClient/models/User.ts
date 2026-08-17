/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { Fido2Id } from './Fido2Id';
import type { UserId } from './UserId';
import type { UserPreferences } from './UserPreferences';
import type { UserRole } from './UserRole';
export type User = {
    id: UserId;
    fido2Id: Fido2Id;
    username: string | null;
    role?: UserRole;
    isDisabled?: boolean;
    preferences?: UserPreferences;
};

