import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Brewery, PagedResponse } from './models';

export interface BrewerySearch {
  search?: string;
  name?: string;
  city?: string;
  sortBy?: string;
  sortDirection?: string;
  latitude?: number;
  longitude?: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class BreweryApiService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = 'http://localhost:5001/api/breweries';

  search(query: BrewerySearch): Observable<PagedResponse<Brewery>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, value);
      }
    }

    return this.http.get<PagedResponse<Brewery>>(this.apiUrl, { params });
  }

  autocomplete(term: string, limit = 10): Observable<string[]> {
    const params = new HttpParams().set('term', term).set('limit', limit);
    return this.http.get<string[]>(`${this.apiUrl}/autocomplete`, { params });
  }
}
