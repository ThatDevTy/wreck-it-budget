# =====================================================================
#  Wreck-It Budget - API test runner
#  Covers WIB-23 (income), WIB-24 (bills), WIB-25 (debts)
#
#  HOW TO RUN
#    1. In one terminal, start the app:
#         cd C:\Users\Basse\source\repos\wreck-it-budget
#         dotnet run --project src/WreckItBudget.Api
#       Leave it running. It is supposed to sit there.
#
#    2. In a SECOND terminal:
#         cd $env:USERPROFILE\OneDrive\Desktop
#         .\api-test.ps1
#
#  HOW TO READ IT
#    Green PASS = behaved as expected.   Red FAIL = a real finding.
#    Every line shows what status code was expected and what came back,
#    with the response body underneath in grey.
#
#    Important: most of these tests EXPECT a failure status. A 400 or a 409
#    is a PASS when that is what the endpoint should have said. Only the
#    colour matters, not the number.
#
#  It cleans up everything it creates, so it can be run over and over.
# =====================================================================

$Base  = "http://localhost:5120"
$Stamp = Get-Date -Format "HHmmss"
$Pass  = 0
$Fail  = 0

function Call($Method, $Path, $Body) {
    try {
        $r = Invoke-WebRequest -Uri "$Base$Path" -Method $Method -ContentType "application/json" `
                               -Body $Body -UseBasicParsing -ErrorAction Stop
        return @{ Code = [int]$r.StatusCode; Text = $r.Content }
    }
    catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { return @{ Code = 0; Text = "NO RESPONSE" } }
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $text = $reader.ReadToEnd(); $reader.Close()
        return @{ Code = [int]$resp.StatusCode; Text = $text }
    }
}

function Check($Label, $Expected, $Method, $Path, $Body) {
    $r = Call $Method $Path $Body
    $ok = ($r.Code -eq $Expected)
    if ($ok) { $script:Pass++ } else { $script:Fail++ }

    Write-Host ("  {0}  {1,-38} want {2}  got {3}" -f $(if($ok){"PASS"}else{"FAIL"}), $Label, $Expected, $r.Code) `
               -ForegroundColor $(if($ok){"Green"}else{"Red"})

    $b = $r.Text
    if ($b.Length -gt 140) { $b = $b.Substring(0,140) + " ..." }
    if ($b) { Write-Host ("          {0}" -f ($b -replace "`r?`n"," ")) -ForegroundColor DarkGray }

    return $r
}

function Section($Title, $Why) {
    Write-Host ""
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host "  $Why" -ForegroundColor DarkCyan
    Write-Host ""
}

function Banner($Text) {
    Write-Host ""
    Write-Host ("=" * 72) -ForegroundColor Yellow
    Write-Host "  $Text" -ForegroundColor Yellow
    Write-Host ("=" * 72) -ForegroundColor Yellow
}

function Json($r) { try { $r.Text | ConvertFrom-Json } catch { $null } }
function IdOf($r) { $j = Json $r; if ($j) { $j.id } else { $null } }

# ---------------------------------------------------------------- preflight
Write-Host ""
$ping = Call GET "/api/income" $null
if ($ping.Code -eq 0) {
    Write-Host "  The API is not responding at $Base" -ForegroundColor Red
    Write-Host "  Start it first:  dotnet run --project src/WreckItBudget.Api" -ForegroundColor Red
    Write-Host ""
    exit 1
}
Write-Host "  API is up at $Base" -ForegroundColor Green


# =====================================================================
Banner "WIB-23   INCOME SOURCES"

Section "Reading" "A GET on an empty-ish table should still succeed. 200 with a list, even a short one."
Check "list all income" 200 GET "/api/income" $null | Out-Null

Section "Creating valid income" @"
One of each pay type. The three types need DIFFERENT fields, which is the
whole point of the schedule rule: weekly needs a weekday, biweekly needs a
weekday AND an anchor date, semi-monthly needs two days of the month.
"@
$w = Check "weekly"       201 POST "/api/income" "{""name"":""Weekly $Stamp"",""ownerUserId"":7,""payType"":""weekly"",""payWeekday"":5,""amountPerCheck"":320}"
$b = Check "biweekly"     201 POST "/api/income" "{""name"":""Biweekly $Stamp"",""ownerUserId"":7,""payType"":""biweekly"",""payWeekday"":5,""firstPayDate"":""2026-09-11"",""amountPerCheck"":1842}"
$s = Check "semi-monthly" 201 POST "/api/income" "{""name"":""Semi $Stamp"",""ownerUserId"":8,""payType"":""semi-monthly"",""payDay1"":1,""payDay2"":15,""amountPerCheck"":2100}"
$wId = IdOf $w; $bId = IdOf $b; $sId = IdOf $s

Write-Host ""
Write-Host "  Look at the grey line above: ownerName should be a REAL NAME," -ForegroundColor DarkYellow
Write-Host "  not a number, and there should be no 'owner: null' anywhere." -ForegroundColor DarkYellow
Write-Host "  That is the response model doing its job." -ForegroundColor DarkYellow

Section "Fetching one by id" "404 is the correct answer for something that does not exist. It is not an error in your code."
Check "get by id"          200 GET "/api/income/$wId" $null | Out-Null
Check "get by id, missing" 404 GET "/api/income/999999" $null | Out-Null

Section "Rejecting bad income" @"
Each of these SHOULD fail. The point is not just that they fail, but that they
fail with the right status and a message a human can act on - rather than a
500 and a stack trace, which is what happens when the database catches it first.
  400 = your request is nonsense
  409 = your request is fine, but it clashes with what is already here
"@
Check "unknown owner"                400 POST "/api/income" "{""name"":""Ghost $Stamp"",""ownerUserId"":999,""payType"":""weekly"",""payWeekday"":5,""amountPerCheck"":100}" | Out-Null
Check "duplicate name"               409 POST "/api/income" "{""name"":""Weekly $Stamp"",""ownerUserId"":7,""payType"":""weekly"",""payWeekday"":3,""amountPerCheck"":100}" | Out-Null
Check "weekly, no weekday"           400 POST "/api/income" "{""name"":""E1 $Stamp"",""ownerUserId"":7,""payType"":""weekly"",""amountPerCheck"":320}" | Out-Null
Check "weekly carrying a payDay"     400 POST "/api/income" "{""name"":""E2 $Stamp"",""ownerUserId"":7,""payType"":""weekly"",""payWeekday"":5,""payDay1"":15,""amountPerCheck"":320}" | Out-Null
Check "biweekly, no anchor date"     400 POST "/api/income" "{""name"":""E3 $Stamp"",""ownerUserId"":7,""payType"":""biweekly"",""payWeekday"":5,""amountPerCheck"":1800}" | Out-Null
Check "biweekly carrying payDays"    400 POST "/api/income" "{""name"":""E4 $Stamp"",""ownerUserId"":7,""payType"":""biweekly"",""payWeekday"":5,""firstPayDate"":""2026-09-11"",""payDay1"":1,""payDay2"":15,""amountPerCheck"":1800}" | Out-Null
Check "semi-monthly, only one day"   400 POST "/api/income" "{""name"":""E5 $Stamp"",""ownerUserId"":7,""payType"":""semi-monthly"",""payDay1"":1,""amountPerCheck"":2100}" | Out-Null
Check "semi-monthly with a weekday"  400 POST "/api/income" "{""name"":""E6 $Stamp"",""ownerUserId"":7,""payType"":""semi-monthly"",""payDay1"":1,""payDay2"":15,""payWeekday"":5,""amountPerCheck"":2100}" | Out-Null
Check "semi-monthly, days backwards" 400 POST "/api/income" "{""name"":""E7 $Stamp"",""ownerUserId"":7,""payType"":""semi-monthly"",""payDay1"":15,""payDay2"":1,""amountPerCheck"":2100}" | Out-Null
Check "unknown pay type"             400 POST "/api/income" "{""name"":""E8 $Stamp"",""ownerUserId"":7,""payType"":""monthly"",""amountPerCheck"":500}" | Out-Null

Section "Updating" @"
The second test is the subtle one. Saving a record WITHOUT renaming it must
not report 'that name is taken' - taken by itself. That is what the
'and Id != id' clause in the duplicate check is for.
"@
Check "put, valid"                 204 PUT "/api/income/$wId" "{""name"":""Weekly $Stamp"",""ownerUserId"":7,""payType"":""weekly"",""payWeekday"":3,""amountPerCheck"":400,""isActive"":true}" | Out-Null
Check "put, keeping its own name"  204 PUT "/api/income/$wId" "{""name"":""Weekly $Stamp"",""ownerUserId"":7,""payType"":""weekly"",""payWeekday"":3,""amountPerCheck"":400,""isActive"":false}" | Out-Null
Check "put, name owned by another" 409 PUT "/api/income/$wId" "{""name"":""Semi $Stamp"",""ownerUserId"":7,""payType"":""weekly"",""payWeekday"":3,""amountPerCheck"":400,""isActive"":true}" | Out-Null
Check "put, missing id"            404 PUT "/api/income/999999" "{""name"":""Nope $Stamp"",""ownerUserId"":7,""payType"":""weekly"",""payWeekday"":3,""amountPerCheck"":400,""isActive"":true}" | Out-Null
Check "put, bad schedule"          400 PUT "/api/income/$wId" "{""name"":""Weekly $Stamp"",""ownerUserId"":7,""payType"":""weekly"",""payWeekday"":3,""payDay1"":9,""amountPerCheck"":400,""isActive"":true}" | Out-Null

Section "Deleting" "204 means 'done, nothing to say'. Deleting the same thing twice gives 404, which is correct - it really is not there any more."
Check "delete"              204 DELETE "/api/income/$wId" $null | Out-Null
Check "delete again"        404 DELETE "/api/income/$wId" $null | Out-Null
Check "tidy up biweekly"    204 DELETE "/api/income/$bId" $null | Out-Null
Check "tidy up semi"        204 DELETE "/api/income/$sId" $null | Out-Null


# =====================================================================
Banner "WIB-24   BILLS"

Section "Creating valid bills" @"
Watch the third one. A bill due on the 31st is ALLOWED on purpose. Whether
the 31st exists depends on the month, and this row has no idea what month it
is - so resolving it (clamping to the last day) is application logic, not a
database rule. Rejecting it here would be wrong.
"@
Check "list all bills" 200 GET "/api/bills" $null | Out-Null
$e = Check "monthly bill"      201 POST "/api/bills" "{""name"":""Electric $Stamp"",""amountDue"":142.50,""payCycle"":""monthly"",""dueDay"":15,""category"":""Utilities""}"
$n = Check "no category"       201 POST "/api/bills" "{""name"":""Internet $Stamp"",""amountDue"":79.99,""payCycle"":""monthly"",""dueDay"":1,""category"":null}"
$r = Check "due on the 31st"   201 POST "/api/bills" "{""name"":""Rent $Stamp"",""amountDue"":1450,""payCycle"":""monthly"",""dueDay"":31,""category"":null}"
$eId = IdOf $e; $nId = IdOf $n; $rId = IdOf $r

Check "get by id"          200 GET "/api/bills/$eId" $null | Out-Null
Check "get by id, missing" 404 GET "/api/bills/999999" $null | Out-Null

Section "Rejecting bad bills" @"
Note the amount rule here is 'greater than 0' - a zero-pound bill is a typo.
Compare that with DEBTS further down, where a zero BALANCE is the goal and
has to be allowed. Same-looking rule, opposite answer.
"@
Check "duplicate name"     409 POST "/api/bills" "{""name"":""Electric $Stamp"",""amountDue"":50,""payCycle"":""monthly"",""dueDay"":1,""category"":null}" | Out-Null
Check "zero amount"        400 POST "/api/bills" "{""name"":""Free $Stamp"",""amountDue"":0,""payCycle"":""monthly"",""dueDay"":1,""category"":null}" | Out-Null
Check "negative amount"    400 POST "/api/bills" "{""name"":""Neg $Stamp"",""amountDue"":-10,""payCycle"":""monthly"",""dueDay"":1,""category"":null}" | Out-Null
Check "unknown pay cycle"  400 POST "/api/bills" "{""name"":""Odd $Stamp"",""amountDue"":10,""payCycle"":""fortnightly"",""dueDay"":1,""category"":null}" | Out-Null
Check "due day 0"          400 POST "/api/bills" "{""name"":""D0 $Stamp"",""amountDue"":10,""payCycle"":""monthly"",""dueDay"":0,""category"":null}" | Out-Null
Check "due day 45"         400 POST "/api/bills" "{""name"":""D45 $Stamp"",""amountDue"":10,""payCycle"":""monthly"",""dueDay"":45,""category"":null}" | Out-Null

Section "Updating and deleting bills" "Same shape as income. Once the first controller is right, the rest should be boring."
Check "put, valid"                 204 PUT "/api/bills/$eId" "{""name"":""Electric $Stamp"",""amountDue"":150.00,""payCycle"":""monthly"",""dueDay"":16,""category"":""Utilities""}" | Out-Null
Check "put, name owned by another" 409 PUT "/api/bills/$eId" "{""name"":""Internet $Stamp"",""amountDue"":150,""payCycle"":""monthly"",""dueDay"":16,""category"":null}" | Out-Null
Check "put, missing id"            404 PUT "/api/bills/999999" "{""name"":""Nope $Stamp"",""amountDue"":10,""payCycle"":""monthly"",""dueDay"":1,""category"":null}" | Out-Null
Check "put, bad cycle"             400 PUT "/api/bills/$eId" "{""name"":""Electric $Stamp"",""amountDue"":150,""payCycle"":""daily"",""dueDay"":16,""category"":null}" | Out-Null
Check "delete"                     204 DELETE "/api/bills/$eId" $null | Out-Null
Check "delete again"               404 DELETE "/api/bills/$eId" $null | Out-Null
Check "tidy up internet"           204 DELETE "/api/bills/$nId" $null | Out-Null
Check "tidy up rent"               204 DELETE "/api/bills/$rId" $null | Out-Null


# =====================================================================
Banner "WIB-25   DEBTS"

Section "Creating valid debts" @"
Debts are the first endpoint where the SERVER decides things the caller never
sent. Watch the grey lines for two fields you did not provide:

  colorIndex - assigned 0, then 1, then 2. Zero-based, because the frontend
               reads it straight as TOWER_PALETTE[colorIndex].
  priority   - assigned 1, then 2, then 3. A new debt joins the BOTTOM of
               the payoff queue.
"@
Check "list all debts" 200 GET "/api/debts" $null | Out-Null
$d1 = Check "visa"          201 POST "/api/debts" "{""name"":""Visa $Stamp"",""currentBalance"":2400.00,""interestRate"":22.99,""dueDay"":20,""statementDay"":28,""minPayment"":50}"
$d2 = Check "car loan"      201 POST "/api/debts" "{""name"":""Car $Stamp"",""currentBalance"":11500,""interestRate"":6.40,""dueDay"":5,""statementDay"":null,""minPayment"":312}"
$d3 = Check "0% promo card" 201 POST "/api/debts" "{""name"":""Promo $Stamp"",""currentBalance"":800,""interestRate"":0,""dueDay"":12,""statementDay"":null,""minPayment"":25}"
$d1Id = IdOf $d1; $d2Id = IdOf $d2; $d3Id = IdOf $d3

Section "Values that LOOK wrong but are correct" @"
These three all have a zero in them, and all three are legitimate:
  0% APR        - promotional cards are real (tested above)
  $0 minimum    - deferred student loans genuinely have one
  $0 balance    - this is the GOAL. The entire app exists to reach it.

If a 'greater than zero' rule had been used on balance, it would be
impossible to ever pay a debt off. This is the most important boundary
in the whole schema.
"@
$d4 = Check "zero minimum payment" 201 POST "/api/debts" "{""name"":""Deferred $Stamp"",""currentBalance"":5000,""interestRate"":0,""dueDay"":1,""statementDay"":null,""minPayment"":0}"
$d5 = Check "zero balance"         201 POST "/api/debts" "{""name"":""Paid $Stamp"",""currentBalance"":0,""interestRate"":10,""dueDay"":1,""statementDay"":null,""minPayment"":25}"
$d4Id = IdOf $d4; $d5Id = IdOf $d5

Section "Rejecting bad debts" "Negative money is nonsense in every direction. Days still have to be real day numbers."
Check "duplicate name"      409 POST "/api/debts" "{""name"":""Visa $Stamp"",""currentBalance"":100,""interestRate"":10,""dueDay"":1,""statementDay"":null,""minPayment"":10}" | Out-Null
Check "negative balance"    400 POST "/api/debts" "{""name"":""N1 $Stamp"",""currentBalance"":-5,""interestRate"":10,""dueDay"":1,""statementDay"":null,""minPayment"":10}" | Out-Null
Check "negative rate"       400 POST "/api/debts" "{""name"":""N2 $Stamp"",""currentBalance"":100,""interestRate"":-1,""dueDay"":1,""statementDay"":null,""minPayment"":10}" | Out-Null
Check "negative minimum"    400 POST "/api/debts" "{""name"":""N3 $Stamp"",""currentBalance"":100,""interestRate"":10,""dueDay"":1,""statementDay"":null,""minPayment"":-5}" | Out-Null
Check "due day 0"           400 POST "/api/debts" "{""name"":""N4 $Stamp"",""currentBalance"":100,""interestRate"":10,""dueDay"":0,""statementDay"":null,""minPayment"":10}" | Out-Null
Check "due day 45"          400 POST "/api/debts" "{""name"":""N5 $Stamp"",""currentBalance"":100,""interestRate"":10,""dueDay"":45,""statementDay"":null,""minPayment"":10}" | Out-Null
Check "statement day 45"    400 POST "/api/debts" "{""name"":""N6 $Stamp"",""currentBalance"":100,""interestRate"":10,""dueDay"":1,""statementDay"":45,""minPayment"":10}" | Out-Null

Section "Paying a debt off - the interesting one" @"
This is the behaviour that makes DebtsController different from the other two.

The caller never says 'this debt is cleared'. It only sets the balance to 0.
The server works out what that MEANS: mark it cleared, stamp the time, and
remove it from the payoff queue (priority becomes null).

Derive state from facts. A caller can lie about being paid off; a caller
cannot lie about the balance being zero, because the balance is the fact
being checked.
"@
Check "pay the Visa down to 0" 204 PUT "/api/debts/$d1Id" "{""name"":""Visa $Stamp"",""currentBalance"":0,""interestRate"":22.99,""dueDay"":20,""statementDay"":28,""minPayment"":50,""priority"":1}" | Out-Null
$cleared = Check "re-read it"  200 GET "/api/debts/$d1Id" $null

$j = Json $cleared
if ($j) {
    $okCleared  = ($j.isCleared -eq $true)
    $okStamped  = ($null -ne $j.clearedAt)
    $okPriority = ($null -eq $j.priority)
    foreach ($t in @(
        @{ n = "isCleared flipped to true";      ok = $okCleared  },
        @{ n = "clearedAt was stamped";          ok = $okStamped  },
        @{ n = "priority removed from queue";    ok = $okPriority })) {
        if ($t.ok) { $script:Pass++ } else { $script:Fail++ }
        Write-Host ("  {0}  {1}" -f $(if($t.ok){"PASS"}else{"FAIL"}), $t.n) `
                   -ForegroundColor $(if($t.ok){"Green"}else{"Red"})
    }
}

Section "Updating and deleting debts" "Priority must stay inside 1-25, or be null. Twenty-six debts is a tripwire, not a real limit."
Check "put, priority out of range" 400 PUT "/api/debts/$d2Id" "{""name"":""Car $Stamp"",""currentBalance"":11500,""interestRate"":6.4,""dueDay"":5,""statementDay"":null,""minPayment"":312,""priority"":99}" | Out-Null
Check "put, name owned by another" 409 PUT "/api/debts/$d2Id" "{""name"":""Promo $Stamp"",""currentBalance"":11500,""interestRate"":6.4,""dueDay"":5,""statementDay"":null,""minPayment"":312,""priority"":2}" | Out-Null
Check "put, missing id"            404 PUT "/api/debts/999999" "{""name"":""Nope $Stamp"",""currentBalance"":1,""interestRate"":1,""dueDay"":1,""statementDay"":null,""minPayment"":1,""priority"":1}" | Out-Null
Check "delete"                     204 DELETE "/api/debts/$d1Id" $null | Out-Null
Check "delete again"               404 DELETE "/api/debts/$d1Id" $null | Out-Null
Check "tidy up car"                204 DELETE "/api/debts/$d2Id" $null | Out-Null
Check "tidy up promo"              204 DELETE "/api/debts/$d3Id" $null | Out-Null
Check "tidy up deferred"           204 DELETE "/api/debts/$d4Id" $null | Out-Null
Check "tidy up paid"               204 DELETE "/api/debts/$d5Id" $null | Out-Null


# =====================================================================
Write-Host ""
Write-Host ("=" * 72) -ForegroundColor $(if($Fail -eq 0){"Green"}else{"Red"})
Write-Host ("  {0} passed, {1} failed" -f $Pass, $Fail) -ForegroundColor $(if($Fail -eq 0){"Green"}else{"Red"})
if ($Fail -eq 0) {
    Write-Host "  WIB-23, WIB-24 and WIB-25 all behave as specified." -ForegroundColor Green
    Write-Host "  Paste this output into each ticket and move them to Done." -ForegroundColor Green
} else {
    Write-Host "  Red lines are real findings. Send them over and we will work out" -ForegroundColor Red
    Write-Host "  whether the code is wrong or the test is." -ForegroundColor Red
}
Write-Host ("=" * 72) -ForegroundColor $(if($Fail -eq 0){"Green"}else{"Red"})
Write-Host ""
