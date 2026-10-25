/** Photos are resized on the phone before upload: smaller uploads on mobile data, and the server stays simple. */
export const MAX_PHOTO_SIDE = 1600;
export const MAX_UPLOAD_BYTES = 2 * 1024 * 1024;

/** Scales width and height down, keeping the aspect ratio, so the longer side is at most `max`. */
export function fitWithin(
  width: number,
  height: number,
  max: number,
): { width: number; height: number } {
  const scale = Math.min(1, max / Math.max(width, height));
  return { width: Math.round(width * scale), height: Math.round(height * scale) };
}

export async function resizeImage(file: File, max = MAX_PHOTO_SIDE, quality = 0.82): Promise<Blob> {
  if (!file.type.startsWith('image/')) {
    throw new Error('Choose a photo.');
  }

  let resized: Blob | null = null;
  try {
    const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
    const size = fitWithin(bitmap.width, bitmap.height, max);
    const canvas = document.createElement('canvas');
    canvas.width = size.width;
    canvas.height = size.height;
    canvas.getContext('2d')!.drawImage(bitmap, 0, 0, size.width, size.height);
    bitmap.close();
    resized = await new Promise<Blob | null>((resolve) =>
      canvas.toBlob(resolve, 'image/jpeg', quality),
    );
  } catch {
    resized = null;
  }

  // Browsers that cannot decode the photo (some HEIC files) fall back to the original if small enough.
  const result = resized ?? file;
  if (result.size > MAX_UPLOAD_BYTES) {
    throw new Error('The photo is too large. Please use one under 2 MB.');
  }
  return result;
}
