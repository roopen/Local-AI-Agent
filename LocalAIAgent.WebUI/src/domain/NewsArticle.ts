class NewsArticle {
    readonly Title: string;
    readonly Summary: string;
    readonly PublishDate: Date;
    readonly Link: string;
    readonly Source: string;

    constructor(title: string, summary: string, publishDate: Date, link: string, source: string) {
        this.Title = title;
        this.Summary = summary;
        this.PublishDate = publishDate;
        this.Link = link;
        this.Source = source;
    }
}

export default NewsArticle;