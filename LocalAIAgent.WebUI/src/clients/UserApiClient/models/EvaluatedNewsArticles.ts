/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { NewsArticle } from './NewsArticle';
export type EvaluatedNewsArticles = {
    newsArticles: Array<NewsArticle> | null;
    readonly highRelevancyPercentage?: number;
    readonly mediumRelevancyPercentage?: number;
    lowRelevancyPercentage?: number;
};

