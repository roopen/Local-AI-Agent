/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AiSettingsResponse } from '../models/AiSettingsResponse';
import type { UpdateAiSettingsRequest } from '../models/UpdateAiSettingsRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class AiSettingsService {
    /**
     * @returns AiSettingsResponse OK
     * @throws ApiError
     */
    public static getApiAiSettings(): CancelablePromise<AiSettingsResponse> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/ai-settings',
        });
    }
    /**
     * @param requestBody
     * @returns AiSettingsResponse OK
     * @throws ApiError
     */
    public static putApiAiSettings(
        requestBody?: UpdateAiSettingsRequest,
    ): CancelablePromise<AiSettingsResponse> {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/ai-settings',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
