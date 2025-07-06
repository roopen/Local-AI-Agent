/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { EvaluatedNewsArticles } from '../models/EvaluatedNewsArticles';
import type { NewsItem } from '../models/NewsItem';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class NewsService {
    /**
     * @param userId
     * @returns NewsItem OK
     * @throws ApiError
     */
    public static getApiNews(
        userId: number,
    ): CancelablePromise<Array<NewsItem>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/News/{userId}',
            path: {
                'userId': userId,
            },
        });
    }
    /**
     * @param userId
     * @returns EvaluatedNewsArticles OK
     * @throws ApiError
     */
    public static postApiNewsGetNewsV2(
        userId?: number,
    ): CancelablePromise<EvaluatedNewsArticles> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/News/GetNewsV2',
            query: {
                'userId': userId,
            },
        });
    }
    /**
     * @param userId
     * @returns any OK
     * @throws ApiError
     */
    public static getApiNewsNewsStream(
        userId?: number,
    ): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/News/newsStream',
            query: {
                'userId': userId,
            },
        });
    }
}
