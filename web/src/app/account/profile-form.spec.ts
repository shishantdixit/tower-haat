import { SaveProfileRequest } from '../core/api/models';
import { validate } from './profile-form';

describe('profile form validation', () => {
  const valid: SaveProfileRequest = {
    name: 'Asha',
    societyId: 's1',
    flatId: 'f1',
    manualTower: null,
    manualFlat: null,
    consent: true,
  };

  it('accepts a listed flat', () => {
    expect(validate(valid, false)).toBeNull();
  });

  it('accepts a typed tower and flat', () => {
    expect(
      validate({ ...valid, flatId: null, manualTower: 'C', manualFlat: '1204' }, false),
    ).toBeNull();
  });

  it('needs a name, a flat and consent at sign-up', () => {
    expect(validate({ ...valid, name: 'A' }, false)).toContain('name');
    expect(validate({ ...valid, flatId: null, manualTower: 'C' }, false)).toContain('flat');
    expect(validate({ ...valid, consent: false }, false)).toContain('agree');
  });

  it('does not ask for consent again when editing', () => {
    expect(validate({ ...valid, consent: false }, true)).toBeNull();
  });
});
