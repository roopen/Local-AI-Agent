import type { Relevancy } from "./Relevancy";

class NewsArticle {
    readonly Title: string;
    readonly Summary: string;
    readonly PublishDate: Date;
    readonly Link: string;
    readonly Source: string;
    readonly Categories: string[];
    readonly Relevancy: Relevancy;
    readonly Reasoning: string | null;

    constructor(
        title: string,
        summary: string,
        publishDate: Date,
        link: string,
        source: string,
        categories: string[],
        relevancy: Relevancy,
        reasoning: string | null
    ) {
        this.Title = title;
        this.Summary = summary;
        this.PublishDate = publishDate;
        this.Link = link;
        this.Source = source;
        this.Categories = categories;
        this.Relevancy = relevancy;
        this.Reasoning = reasoning;
    }
}

export default NewsArticle;