import { isImageUploadUrl } from './upload-url.util';

describe('upload-url.util', () => {
  it('detects Azure image URL with query string', () => {
    const url = 'https://account.blob.core.windows.net/container/vehicles/1/54/abc.jpeg?sv=2024&sig=xyz';
    expect(isImageUploadUrl(url)).toBeTrue();
  });
});
