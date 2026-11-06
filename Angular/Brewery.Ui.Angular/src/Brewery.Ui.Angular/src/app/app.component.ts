import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from './auth.service';
import { BreweryApiService, BrewerySearch } from './brewery-api.service';
import { Brewery, PagedResponse } from './models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  private readonly auth = inject(AuthService);
  private readonly breweryApi = inject(BreweryApiService);

  username = '';
  password = '';
  search = '';
  name = '';
  city = '';
  sortBy = '';
  sortDirection = 'asc';
  latitude?: number;
  longitude?: number;
  autocompleteTerm = '';
  autocompleteLimit = 10;
  suggestions: string[] = [];
  breweries: Brewery[] = [];
  page = 1;
  pageSize = 50;
  totalCount = 0;
  errorMessage = '';
  loading = false;

  get isAuthenticated(): boolean {
    return this.auth.token !== null;
  }

  get canUseAutocomplete(): boolean {
    return this.auth.canUseAutocomplete === true;
  }

  get autocompletePermissionUnknown(): boolean {
    return this.auth.canUseAutocomplete === null;
  }

  signIn(): void {
    this.errorMessage = '';
    this.auth.signIn({ username: this.username, password: this.password }).subscribe({
      next: () => {
        this.password = '';
        this.loadBreweries();
      },
      error: error => this.showError(error)
    });
  }

  signOut(): void {
    this.auth.signOut();
    this.breweries = [];
    this.suggestions = [];
    this.errorMessage = '';
  }

  loadBreweries(page = 1): void {
    this.loading = true;
    this.errorMessage = '';
    this.page = page;
    const query: BrewerySearch = {
      search: this.search || undefined,
      name: this.name || undefined,
      city: this.city || undefined,
      sortBy: this.sortBy || undefined,
      sortDirection: this.sortDirection,
      latitude: this.latitude,
      longitude: this.longitude,
      page,
      pageSize: this.pageSize
    };

    this.breweryApi.search(query).subscribe({
      next: (response: PagedResponse<Brewery>) => {
        this.breweries = response.breweries;
        this.totalCount = response.totalCount ?? response.breweries.length;
        this.loading = false;
      },
      error: error => {
        this.loading = false;
        this.showError(error);
      }
    });
  }

  loadSuggestions(): void {
    if (!this.canUseAutocomplete || this.autocompleteTerm.trim().length < 2) {
      this.suggestions = [];
      return;
    }

    this.breweryApi.autocomplete(this.autocompleteTerm, this.autocompleteLimit).subscribe({
      next: suggestions => {
        this.suggestions = suggestions;
        this.errorMessage = '';
      },
      error: (error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status === 403) {
          this.auth.disableAutocomplete();
          this.suggestions = [];
          this.errorMessage = '';
          return;
        }

        this.showError(error);
      }
    });
  }

  chooseSuggestion(value: string): void {
    this.search = value;
    this.autocompleteTerm = value;
    this.suggestions = [];
    this.loadBreweries();
  }

  get hasPreviousPage(): boolean {
    return this.page > 1;
  }

  get hasNextPage(): boolean {
    return this.page * this.pageSize < this.totalCount;
  }

  private showError(error: unknown): void {
    if (error instanceof HttpErrorResponse) {
      this.errorMessage = error.error?.detail ?? `Request failed (${error.status}).`;
      return;
    }

    this.errorMessage = 'An unexpected request error occurred.';
  }
}
