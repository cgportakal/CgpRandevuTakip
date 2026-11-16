import { TextDecoder, TextEncoder } from 'text-encoding';

/**
 * SignalR WebSocket taşıyıcısı TextEncoder/TextDecoder bekler; Hermes'te her sürümde
 * bulunmadığı için polyfill uygulanır. Varsa mevcut global korunur.
 */
const target = globalThis as unknown as {
  TextEncoder?: unknown;
  TextDecoder?: unknown;
};

if (!target.TextEncoder) target.TextEncoder = TextEncoder;
if (!target.TextDecoder) target.TextDecoder = TextDecoder;
