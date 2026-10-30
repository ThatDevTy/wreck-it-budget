import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Trend } from './trend';

describe('Trend', () => {
  let component: Trend;
  let fixture: ComponentFixture<Trend>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Trend],
    }).compileComponents();

    fixture = TestBed.createComponent(Trend);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
