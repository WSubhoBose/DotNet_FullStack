export interface TokenRequest {
  username: string;
  password: string;
}

export interface TokenResponse {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  canUseAutocomplete: boolean;
}

export interface Brewery {
  id: string;
  name: string;
  breweryType: string;
  address1: string | null;
  city: string;
  stateProvince: string;
  country: string;
  websiteUrl: string | null;
  distance: string | null;
}

export interface PagedResponse<T> {
  breweries: T[];
  page: number;
  pageSize: number;
  totalCount: number | null;
}
