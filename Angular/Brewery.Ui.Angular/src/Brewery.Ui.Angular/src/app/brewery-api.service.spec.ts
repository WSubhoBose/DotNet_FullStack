import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { BreweryApiService } from './brewery-api.service';

describe('BreweryApiService', () => {
  let service: BreweryApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [BreweryApiService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(BreweryApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends v2 brewery searches through the API gateway with filters and paging', () => {
    service.search({
      search: 'beer',
      name: 'Acme',
      city: 'Denver',
      sortBy: 'distance',
      sortDirection: 'desc',
      latitude: 39.7392,
      longitude: -104.9903,
      page: 2,
      pageSize: 20
    }).subscribe();
    const request = http.expectOne(
      request => request.url === 'http://localhost:5001/api/breweries' &&
        request.params.get('search') === 'beer' &&
        request.params.get('name') === 'Acme' &&
        request.params.get('city') === 'Denver' &&
        request.params.get('sortBy') === 'distance' &&
        request.params.get('sortDirection') === 'desc' &&
        request.params.get('latitude') === '39.7392' &&
        request.params.get('longitude') === '-104.9903' &&
        request.params.get('page') === '2' &&
        request.params.get('pageSize') === '20'
    );

    expect(request.request.method).toBe('GET');
    request.flush({ breweries: [], page: 2, pageSize: 20, totalCount: 0 });
  });

  it('requests autocomplete from the v2 gateway route', () => {
    service.autocomplete('den', 5).subscribe();
    const request = http.expectOne(
      request => request.url === 'http://localhost:5001/api/breweries/autocomplete' &&
        request.params.get('term') === 'den' &&
        request.params.get('limit') === '5'
    );

    expect(request.request.method).toBe('GET');
    request.flush(['Denver']);
  });
});
