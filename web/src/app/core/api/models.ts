export interface Profile {
  societyId: string;
  societyName: string;
  flatId: string | null;
  tower: string;
  flat: string;
  isListedFlat: boolean;
}

export interface Me {
  id: string;
  phone: string;
  name: string | null;
  isPlatformAdmin: boolean;
  profile: Profile | null;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: Me;
}

export interface OtpRequested {
  expiresInSeconds: number;
  resendAfterSeconds: number;
  developmentCode: string | null;
}

export interface SaveProfileRequest {
  name: string;
  societyId: string;
  flatId: string | null;
  manualTower: string | null;
  manualFlat: string | null;
  consent: boolean;
}

export interface Society {
  id: string;
  name: string;
  city: string;
}

export interface Flat {
  id: string;
  number: string;
}

export interface Tower {
  id: string;
  name: string;
  flats: Flat[];
}

export interface AdminSociety {
  id: string;
  name: string;
  city: string;
  isActive: boolean;
  towers: number;
  flats: number;
  residents: number;
}

export interface ImportFlatsResult {
  towersCreated: number;
  flatsCreated: number;
  flatsAlreadyListed: number;
  errors: { line: number; message: string }[];
}
