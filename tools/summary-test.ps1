# =====================================================================
#  WIB-26 - Family Score endpoint
#
#   cd $env:USERPROFILE\OneDrive\Desktop
#   .\summary-test.ps1
#
#  This one is different from api-test.ps1. That script checks BEHAVIOUR -
#  did the right status code come back. This checks ARITHMETIC, to the penny.
#
#  It wipes income, bills and debts, seeds values chosen so the maths works
#  out to round numbers, then compares the endpoint against figures you can
#  verify by hand. Then it puts the database back the way it found it (empty).
#
#  WARNING: it deletes all income, bills and debts first. Fine right now -
#  there is no real data in there yet. Do NOT run it after WIB-29, when you
#  have entered your actual household budget.
# =====================================================================

$Base = "http://localhost:5120"
$Pass = 0
$Fail = 0

function Call($Method, $Path, $Body) {
    try {
        $r = Invoke-WebRequest -Uri "$Base$Path" -Method $Method -ContentType "application/json" `
                               -Body $Body -UseBasicParsing -ErrorAction Stop
        return @{ Code = [int]$r.StatusCode; Text = $r.Content }
    } catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { return @{ Code = 0; Text = "NO RESPONSE" } }
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $text = $reader.ReadToEnd(); $reader.Close()
        return @{ Code = [int]$resp.StatusCode; Text = $text }
    }
}

function Expect($Label, $Expected, $Actual) {
    $ok = ([decimal]$Expected -eq [decimal]$Actual)
    if ($ok) { $script:Pass++ } else { $script:Fail++ }
    Write-Host ("  {0}  {1,-28} expected {2,12}   got {3,12}" -f `
        $(if($ok){"PASS"}else{"FAIL"}), $Label, $Expected, $Actual) `
        -ForegroundColor $(if($ok){"Green"}else{"Red"})
}

function WipeAll {
    foreach ($path in @("/api/income", "/api/bills", "/api/debts")) {
        $r = Call GET $path $null
        if ($r.Code -ne 200) { continue }
        $items = $r.Text | ConvertFrom-Json
        foreach ($it in $items) { Call DELETE "$path/$($it.id)" $null | Out-Null }
    }
}

# ---------------------------------------------------------------- preflight
Write-Host ""
if ((Call GET "/api/summary" $null).Code -eq 0) {
    Write-Host "  The API is not responding. Start it first." -ForegroundColor Red
    exit 1
}

# =====================================================================
Write-Host "  TEST 1 - an empty budget" -ForegroundColor Cyan
Write-Host "  Everything zero, and the score zero rather than an error." -ForegroundColor DarkCyan
Write-Host ""
WipeAll
$s = (Call GET "/api/summary" $null).Text | ConvertFrom-Json
Expect "monthly income"   0 $s.monthlyIncome
Expect "monthly bills"    0 $s.monthlyBills
Expect "min debt payments" 0 $s.monthlyMinDebtPayments
Expect "total debt"       0 $s.totalDebtBalance
Expect "family score"     0 $s.familyScore

# =====================================================================
Write-Host ""
Write-Host "  TEST 2 - the arithmetic" -ForegroundColor Cyan
Write-Host @"
  Seeded so you can check it by hand:

    INCOME    weekly       `$100   ->  100 x 52 / 12  =   433.33
              biweekly    `$1000   -> 1000 x 26 / 12  =  2166.67
              semi-mthly  `$1200   -> 1200 x 24 / 12  =  2400.00
                                                    ------------
                                                       5000.00

    BILLS     monthly      `$500   ->                     500.00
              weekly       `$100   ->  100 x 52 / 12  =   433.33
              yearly      `$1200   ->  1200 / 12      =   100.00
                                                    ------------
                                                       1033.33

    DEBTS     minimums       50 + 312                 =   362.00
              balances     2400 + 11500               = 13900.00

    SCORE     5000.00 - 1033.33 - 362.00              =  3604.67

  If 'biweekly' had been treated as twice a month, income would come out
  4833.33 instead of 5000.00 - short by `$166.67 a month, from one wrong
  multiplier.
"@ -ForegroundColor DarkCyan
Write-Host ""

Call POST "/api/income" '{"name":"W","ownerUserId":7,"payType":"weekly","payWeekday":5,"amountPerCheck":100}' | Out-Null
Call POST "/api/income" '{"name":"B","ownerUserId":7,"payType":"biweekly","payWeekday":5,"firstPayDate":"2026-09-11","amountPerCheck":1000}' | Out-Null
Call POST "/api/income" '{"name":"S","ownerUserId":8,"payType":"semi-monthly","payDay1":1,"payDay2":15,"amountPerCheck":1200}' | Out-Null

Call POST "/api/bills" '{"name":"BM","amountDue":500,"payCycle":"monthly","dueDay":1,"category":null}' | Out-Null
Call POST "/api/bills" '{"name":"BW","amountDue":100,"payCycle":"weekly","dueDay":1,"category":null}' | Out-Null
Call POST "/api/bills" '{"name":"BY","amountDue":1200,"payCycle":"yearly","dueDay":1,"category":null}' | Out-Null

Call POST "/api/debts" '{"name":"D1","currentBalance":2400,"interestRate":22.99,"dueDay":20,"statementDay":null,"minPayment":50}' | Out-Null
Call POST "/api/debts" '{"name":"D2","currentBalance":11500,"interestRate":6.4,"dueDay":5,"statementDay":null,"minPayment":312}' | Out-Null

$s = (Call GET "/api/summary" $null).Text | ConvertFrom-Json
Expect "monthly income"    5000.00 $s.monthlyIncome
Expect "monthly bills"     1033.33 $s.monthlyBills
Expect "min debt payments"  362.00 $s.monthlyMinDebtPayments
Expect "total debt"       13900.00 $s.totalDebtBalance
Expect "family score"      3604.67 $s.familyScore

# =====================================================================
Write-Host ""
Write-Host "  TEST 3 - inactive income is excluded" -ForegroundColor Cyan
Write-Host "  Switching the `$1000 biweekly source off should drop income by 2166.67." -ForegroundColor DarkCyan
Write-Host ""
$b = ((Call GET "/api/income" $null).Text | ConvertFrom-Json) | Where-Object { $_.name -eq "B" }
Call PUT "/api/income/$($b.id)" '{"name":"B","ownerUserId":7,"payType":"biweekly","payWeekday":5,"firstPayDate":"2026-09-11","amountPerCheck":1000,"isActive":false}' | Out-Null
$s = (Call GET "/api/summary" $null).Text | ConvertFrom-Json
Expect "monthly income"    2833.33 $s.monthlyIncome
Expect "family score"      1438.00 $s.familyScore

# =====================================================================
Write-Host ""
Write-Host "  TEST 4 - a cleared debt stops counting" -ForegroundColor Cyan
Write-Host "  Paying D2 (`$11,500 / `$312 min) to zero should remove both." -ForegroundColor DarkCyan
Write-Host ""
$d2 = ((Call GET "/api/debts" $null).Text | ConvertFrom-Json) | Where-Object { $_.name -eq "D2" }
Call PUT "/api/debts/$($d2.id)" '{"name":"D2","currentBalance":0,"interestRate":6.4,"dueDay":5,"statementDay":null,"minPayment":312,"priority":2}' | Out-Null
$s = (Call GET "/api/summary" $null).Text | ConvertFrom-Json
Expect "min debt payments"   50.00 $s.monthlyMinDebtPayments
Expect "total debt"        2400.00 $s.totalDebtBalance
Expect "family score"      1750.00 $s.familyScore

# =====================================================================
Write-Host ""
Write-Host "  TEST 5 - the score goes negative, honestly" -ForegroundColor Cyan
Write-Host @"
  Adding a `$5000/month bill puts outgoings well past income. The score must
  come back NEGATIVE - not clamped at zero, not an absolute value. A budgeting
  app that will not tell you that you are underwater is worse than no app.
"@ -ForegroundColor DarkCyan
Write-Host ""
Call POST "/api/bills" '{"name":"BIG","amountDue":5000,"payCycle":"monthly","dueDay":1,"category":null}' | Out-Null
$s = (Call GET "/api/summary" $null).Text | ConvertFrom-Json
Expect "monthly bills"     6033.33 $s.monthlyBills
Expect "family score"     -3250.00 $s.familyScore

# =====================================================================
Write-Host ""
Write-Host "  Cleaning up..." -ForegroundColor DarkGray
WipeAll
$s = (Call GET "/api/summary" $null).Text | ConvertFrom-Json
Expect "back to zero"            0 $s.familyScore

Write-Host ""
Write-Host ("=" * 70) -ForegroundColor $(if($Fail -eq 0){"Green"}else{"Red"})
Write-Host ("  {0} passed, {1} failed" -f $Pass, $Fail) -ForegroundColor $(if($Fail -eq 0){"Green"}else{"Red"})
if ($Fail -eq 0) { Write-Host "  WIB-26 computes correctly to the penny." -ForegroundColor Green }
Write-Host ("=" * 70) -ForegroundColor $(if($Fail -eq 0){"Green"}else{"Red"})
Write-Host ""
