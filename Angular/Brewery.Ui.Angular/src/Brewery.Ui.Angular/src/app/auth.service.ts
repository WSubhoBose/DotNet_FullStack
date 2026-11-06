import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { TokenRequest, TokenResponse } from './models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly tokenKey = 'brewery-api-token';
  private readonly autocompletePermissionKey = 'brewery-can-use-autocomplete';
  private readonly gatewayUrl = 'http://localhost:5001';

  get token(): string | null {
    return sessionStorage.getItem(this.tokenKey);
  }

  get canUseAutocomplete(): boolean | null {
    const permission = sessionStorage.getItem(this.autocompletePermissionKey);
    return permission === null ? null : permission === 'true';
  }

  signIn(request: TokenRequest): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${this.gatewayUrl}/api/auth/token`, request).pipe(
      tap(response => {
        sessionStorage.setItem(this.tokenKey, response.accessToken);
        sessionStorage.setItem(this.autocompletePermissionKey, String(response.canUseAutocomplete === true));
      })
    );
  }

  disableAutocomplete(): void {
    sessionStorage.setItem(this.autocompletePermissionKey, 'false');
  }

  signOut(): void {
    sessionStorage.removeItem(this.tokenKey);
    sessionStorage.removeItem(this.autocompletePermissionKey);
  }
}
