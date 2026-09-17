/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { ArticleContent } from './ArticleContent';
export type ReadArticleResult = {
    original: ArticleContent;
    translatedMarkdown?: string | null;
    translatedTitle?: string | null;
    detectedLanguage?: string | null;
    targetLanguage: string | null;
    translationStatus?: string | null;
    message?: string | null;
};

