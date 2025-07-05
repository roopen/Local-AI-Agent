/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { Relevancy } from './Relevancy';
export type NewsArticle = {
    title: string | null;
    summary: string | null;
    publishedDate: string;
    link: string | null;
    source: string | null;
    categories: Array<string> | null;
    relevancy: Relevancy;
    reasoning?: string | null;
};

