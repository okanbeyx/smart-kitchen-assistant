import { parseApiErrorResponse } from '@/core/api/apiError';

describe('parseApiErrorResponse', () => {
  it('maps expected ProblemDetails fields', () => {
    const error = parseApiErrorResponse(
      404,
      'application/problem+json',
      JSON.stringify({
        title: 'Recipe not found.',
        detail: 'The requested recipe was not found.',
      }),
    );

    expect(error).toMatchObject({
      kind: 'http',
      status: 404,
      title: 'Recipe not found.',
      detail: 'The requested recipe was not found.',
    });
  });

  it('maps validation error collections', () => {
    const error = parseApiErrorResponse(
      400,
      'application/json; charset=utf-8',
      JSON.stringify({
        title: 'Validation failed.',
        errors: { Quantity: ['Quantity must be greater than zero.'] },
      }),
    );

    expect(error.kind).toBe('validation');
    expect(error.validationErrors).toEqual({
      Quantity: ['Quantity must be greater than zero.'],
    });
  });

  it('preserves a safe business error code', () => {
    const error = parseApiErrorResponse(
      409,
      'application/problem+json',
      JSON.stringify({ code: 'insufficient_stock' }),
    );

    expect(error.code).toBe('insufficient_stock');
  });

  it('returns a parse error for malformed JSON without exposing raw content', () => {
    const rawBody = '<html>SQL exception and stack trace</html>';
    const error = parseApiErrorResponse(500, 'text/html', rawBody);

    expect(error.kind).toBe('parse');
    expect(error.detail).toBeUndefined();
    expect(error.message).not.toContain(rawBody);
    expect(JSON.stringify(error)).not.toContain('SQL exception');
  });
});
