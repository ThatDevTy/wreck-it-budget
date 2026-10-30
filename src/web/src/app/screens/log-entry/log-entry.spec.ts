import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LogEntry } from './log-entry';

describe('LogEntry', () => {
  let component: LogEntry;
  let fixture: ComponentFixture<LogEntry>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LogEntry],
    }).compileComponents();

    fixture = TestBed.createComponent(LogEntry);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
