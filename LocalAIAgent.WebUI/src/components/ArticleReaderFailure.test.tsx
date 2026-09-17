import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import ArticleReaderFailure from './ArticleReaderFailure';

test('diagnostics can be expanded and copied without rendering error text as HTML', async () => {
    const writeText = jest.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });
    const error = { message: 'The publisher could not be reached.',
        details: 'Request ID: request-123\nTool: browser_navigate\nnet::ERR_CONNECTION_REFUSED\n<script>bad()</script>' };
    render(<ArticleReaderFailure error={error} />);
    expect(screen.getByRole('alert').textContent).toContain(error.message);
    fireEvent.click(screen.getByText('Diagnostic details'));
    expect(screen.getByText(/Request ID: request-123/).textContent).toBe(error.details);
    expect(document.querySelector('script')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Copy error details' }));
    await waitFor(() => expect(screen.getByRole('status').textContent).toBe('Copied'));
    expect(writeText).toHaveBeenCalledWith(`${error.message}\n\n${error.details}`);
});
