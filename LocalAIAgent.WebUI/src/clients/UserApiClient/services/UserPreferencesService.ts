/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { UserPreferenceDto } from '../models/UserPreferenceDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class UserPreferencesService {
    /**
     * @returns UserPreferenceDto OK
     * @throws ApiError
     */
    public static getApiUserPreferences(): CancelablePromise<UserPreferenceDto> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/user-preferences',
        });
    }
    /**
     * @param requestBody
     * @returns any OK
     * @throws ApiError
     */
    public static putApiUserPreferences(
        requestBody?: UserPreferenceDto,
    ): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/user-preferences',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
