/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
export type NewsFeedbackDto = {
    userId: number;
    articleLink: string | null;
    articleTitle: string | null;
    articleSummary: string | null;
    articleTopic: string | null;
    isLiked: boolean;
    reason?: string | null;
    selectedLikes?: Array<string> | null;
    selectedDislikes?: Array<string> | null;
};

