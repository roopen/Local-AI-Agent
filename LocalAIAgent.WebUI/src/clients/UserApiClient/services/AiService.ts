/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { LMStudioModel } from '../models/LMStudioModel';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class AiService {
    /**
     * @returns LMStudioModel OK
     * @throws ApiError
     */
    public static getApiAiModels(): CancelablePromise<Array<LMStudioModel>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/AI/models',
        });
    }
    /**
     * @param requestBody
     * @returns any OK
     * @throws ApiError
     */
    public static postApiAiModelsDownload(
        requestBody?: string,
    ): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/AI/models/download',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
