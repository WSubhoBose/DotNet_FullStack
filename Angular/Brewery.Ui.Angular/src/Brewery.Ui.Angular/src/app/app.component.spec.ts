import { of } from 'rxjs';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';
import { BreweryApiService } from './brewery-api.service';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  let canUseAutocomplete: boolean | null;
  let fixture: ComponentFixture<AppComponent>;

  beforeEach(async () => {
    canUseAutocomplete = false;
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        {
          provide: AuthService,
          useValue: {
            token: 'test-token',
            get canUseAutocomplete() {
              return canUseAutocomplete;
            },
            disableAutocomplete: jasmine.createSpy('disableAutocomplete'),
            signOut: jasmine.createSpy('signOut')
          }
        },
        {
          provide: BreweryApiService,
          useValue: {
            search: jasmine.createSpy('search').and.returnValue(
              of({ breweries: [], page: 1, pageSize: 20, totalCount: 0 })
            ),
            autocomplete: jasmine.createSpy('autocomplete').and.returnValue(of([]))
          }
        }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
  });

  it('disables autocomplete inputs for accounts without permission', () => {
    const term = fixture.nativeElement.querySelector('[name="autocompleteTerm"]') as HTMLInputElement;
    const limit = fixture.nativeElement.querySelector('[name="autocompleteLimit"]') as HTMLInputElement;

    expect(term.disabled).toBeTrue();
    expect(limit.disabled).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Autocomplete is unavailable for this account.');
  });

  it('shows every brewery query and autocomplete parameter in the form', () => {
    const inputNames = ['search', 'name', 'city', 'sortBy', 'sortDirection', 'page', 'pageSize', 'autocompleteTerm', 'autocompleteLimit'];
    expect(fixture.componentInstance.page).toBe(1);
    expect(fixture.componentInstance.pageSize).toBe(50);
    for (const name of inputNames) {
      expect(fixture.nativeElement.querySelector(`[name="${name}"]`)).withContext(name).not.toBeNull();
    }

    fixture.componentInstance.sortBy = 'distance';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[name="latitude"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[name="longitude"]')).not.toBeNull();
  });
});
