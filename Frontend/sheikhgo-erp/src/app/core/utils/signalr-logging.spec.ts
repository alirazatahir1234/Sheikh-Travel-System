import { redactSignalRAccessToken } from './signalr-logging';

describe('redactSignalRAccessToken', () => {
  it('redacts access_token query values in WSS URLs', () => {
    const msg =
      'WebSocket connected to wss://example.com/hubs/tracking?access_token=eyJhbGciOi.secret.part&id=abc';
    expect(redactSignalRAccessToken(msg)).toBe(
      'WebSocket connected to wss://example.com/hubs/tracking?access_token=[REDACTED]&id=abc'
    );
  });

  it('leaves messages without tokens unchanged', () => {
    const msg = 'HubConnection connected successfully.';
    expect(redactSignalRAccessToken(msg)).toBe(msg);
  });
});
