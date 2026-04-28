/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { ExpandedNewsResult } from '../models/ExpandedNewsResult';
import type { NewsFeedbackDto } from '../models/NewsFeedbackDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class NewsService {
    /**
     * @param requestBody
     * @returns ExpandedNewsResult OK
     * @throws ApiError
     */
    public static postApiNewsGetExpandedNews(
        requestBody?: string,
    ): CancelablePromise<ExpandedNewsResult> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/News/GetExpandedNews',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param requestBody
     * @returns any OK
     * @throws ApiError
     */
    public static postApiNewsFeedback(
        requestBody?: NewsFeedbackDto,
    ): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/News/Feedback',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns any OK
     * @throws ApiError
     */
    public static getApiNewsDataset(): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/News/Dataset',
        });
    }
}
