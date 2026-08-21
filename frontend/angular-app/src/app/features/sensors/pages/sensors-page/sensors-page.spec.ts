import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { SensorsPage } from './sensors-page';

describe('SensorsPage', () => {
  let component: SensorsPage;
  let fixture: ComponentFixture<SensorsPage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SensorsPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SensorsPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});