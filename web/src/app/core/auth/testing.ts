import { AuthResponse } from '../api/models';

/** Test data for a signed-in user. */
export function authResponse(overrides: Partial<AuthResponse['user']> = {}): AuthResponse {
  return {
    accessToken: 'access-1',
    accessTokenExpiresAt: '2026-01-01T00:30:00Z',
    user: {
      id: 'u1',
      phone: '+919876543210',
      name: 'Asha',
      isPlatformAdmin: false,
      profile: null,
      ...overrides,
    },
  };
}
