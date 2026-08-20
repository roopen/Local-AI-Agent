import { render, screen } from '@testing-library/react';
import { ArticleStatusMessage } from './NewsComponent';

describe('ArticleStatusMessage', () => {
    it('shows the feed-loading phase', () => {
        render(<ArticleStatusMessage loadingPhase="feeds" isLoading filteredCount={0} error={null} dots={2} />);

        expect(screen.getByRole('status').textContent).toBe('Loading news feeds..');
    });

    it('shows the LLM-loading phase', () => {
        render(<ArticleStatusMessage loadingPhase="llm" isLoading filteredCount={0} error={null} dots={1} />);

        expect(screen.getByRole('status').textContent).toBe('Loading LLM.');
    });

    it('shows no loading message after articles start streaming', () => {
        const { container } = render(
            <ArticleStatusMessage loadingPhase={null} isLoading filteredCount={1} error={null} dots={1} />
        );

        expect(container.firstChild).toBeNull();
    });
});
