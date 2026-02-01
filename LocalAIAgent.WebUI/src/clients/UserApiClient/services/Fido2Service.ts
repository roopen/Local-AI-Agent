/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AssertionOptions } from '../models/AssertionOptions';
import type { AttestationResult } from '../models/AttestationResult';
import type { AuthenticatorAssertionRawResponse } from '../models/AuthenticatorAssertionRawResponse';
import type { AuthenticatorAttestationRawResponse } from '../models/AuthenticatorAttestationRawResponse';
import type { CredentialCreateOptions } from '../models/CredentialCreateOptions';
import type { RegisteredPublicKeyCredential } from '../models/RegisteredPublicKeyCredential';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class Fido2Service {
    /**
     * @param username
     * @returns CredentialCreateOptions OK
     * @throws ApiError
     */
    public static postMakeCredentialOptions(
        username?: string,
    ): CancelablePromise<CredentialCreateOptions> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/makeCredentialOptions',
            query: {
                'username': username,
            },
        });
    }
    /**
     * @param requestBody
     * @returns RegisteredPublicKeyCredential OK
     * @throws ApiError
     */
    public static postMakeCredential(
        requestBody?: AuthenticatorAttestationRawResponse,
    ): CancelablePromise<RegisteredPublicKeyCredential> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/makeCredential',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns AssertionOptions OK
     * @throws ApiError
     */
    public static postAssertionOptions(): CancelablePromise<AssertionOptions> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/assertionOptions',
        });
    }
    /**
     * @param requestBody
     * @returns AttestationResult OK
     * @throws ApiError
     */
    public static postMakeAssertion(
        requestBody?: AuthenticatorAssertionRawResponse,
    ): CancelablePromise<AttestationResult> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/makeAssertion',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
