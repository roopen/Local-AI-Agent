/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AuthenticatorTransport } from './AuthenticatorTransport';
export type AttestationResponse = {
    attestationObject: string | null;
    clientDataJSON: string | null;
    transports: Array<AuthenticatorTransport>;
};

