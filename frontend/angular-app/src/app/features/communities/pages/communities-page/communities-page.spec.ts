import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CommunitiesPage } from './communities-page';

describe('CommunitiesPage', () => {
  let component: CommunitiesPage;
  let fixture: ComponentFixture<CommunitiesPage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CommunitiesPage]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CommunitiesPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
