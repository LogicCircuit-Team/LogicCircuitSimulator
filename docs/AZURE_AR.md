# رفع Logic Circuit Simulator على Azure خطوة بخطوة

هذه الطريقة ترفع المشروع من جهازك مباشرة. لا يلزم رفعه إلى GitHub أولاً. بعد نجاحها تحصل على رابط HTTPS يفتح من الكمبيوتر والهاتف عبر الإنترنت، ويعمل Yosys على السيرفر. أوقف التشغيل المحلي فقط عندما تنتهي من اختباره؛ لا يحتاج الزائر تشغيل PowerShell أو تنزيل Yosys.

## 1. افتح النسخة الصحيحة

فك ZIP الجديد، وافتح PowerShell داخل المجلد الذي يحتوي `LogiclyInspiredSimulator.slnx` و`Dockerfile` و`scripts`. لا تنفّذ أوامر من داخل `CircuitApi`، ولا تنسخ `PS C:\...>` أو سجل تشغيل التطبيق مع الأمر.

جرّب المشروع محلياً أولاً:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Start-Local.ps1
```

افتح https://localhost:7178. جرّب ملف `examples/full_adder.circuit.json`، زر Truth Table، الحفظ والتحميل. لإيقاف التشغيل اضغط Ctrl+C في نافذته. استيراد ملف SV محلياً يحتاج OSS CAD Suite الموجود عندك؛ أما حاوية Azure فتجلب نسخة Linux بنفسها.

## 2. تأكد من الاشتراك والمنطقة

في https://portal.azure.com افتح Subscriptions، وتأكد أن اشتراكك Enabled وأن رصيد الطالب فعال إن كنت تستخدمه. حساب الجامعة وحده لا يثبت وجود اشتراك Azure نشط. الموارد المختارة هي App Service Linux B1 وAzure SQL Basic وContainer Registry Basic؛ لها تكلفة وتستهلك الرصيد المتاح. لا تزل Spending limit لتجاوز خطأ نشر.

افتح Cost Management وأنشئ تنبيه Budget مناسباً لرصيدك. التنبيه ليس إيقافاً تلقائياً. احتفظ بكل موارد التجربة داخل Resource group واحدة لتتعرف عليها لاحقاً. إيقاف Web App لا يوقف بالضرورة رسوم SQL وخطة الاستضافة والريجستري.

اختر منطقة يسمح بها اشتراكك للخدمات الثلاث. `westeurope` مجرد مثال، وليس ضمان توفر في حساب الطالب. خطأ `RequestDisallowedByAzure` أو `RequestDisallowedByPolicy` يعني مراجعة تفاصيل سياسة المنطقة في Azure. `QuotaExceeded` أو نقص الحصة يحتاج مراجعة الحصة أو خطة متاحة، وليس تغيير كود المحاكاة.

## 3. نزّل الأدوات على Windows

نزّل Docker Desktop من https://docs.docker.com/desktop/setup/install/windows-install/، وشغّله باستخدام Linux containers وWSL 2 حسب متطلبات جهازك. أعد التشغيل إذا طلب التثبيت ذلك. افتح Docker Desktop واتركه شغالاً.

ثبّت Azure CLI من Microsoft. يمكنك تنفيذ:

```powershell
winget install --exact --id Microsoft.AzureCLI
```

بعد التثبيت أغلق PowerShell وافتح نافذة جديدة داخل مجلد المشروع. افحص:

```powershell
az version
docker version
docker info --format '{{.OSType}}'
```

السطر الأخير يجب أن يعرض `linux`. إذا لم يعمل `docker`، شغّل Docker Desktop قبل متابعة الخطوات. يحتاج البناء اتصال إنترنت ومساحة كافية لتحميل .NET ومجموعة OSS CAD Suite الكاملة.

## 4. سجّل الدخول واختر الاشتراك

```powershell
az login
az account list --output table
```

خذ ID اشتراكك المقصود من الجدول، وضعه بدلاً من النص بين علامتي التنصيص:

```powershell
az account set --subscription "YOUR-SUBSCRIPTION-ID"
az account show --output table
```

إذا طلب المتصفح الدخول، اختر الحساب الذي يملك الاشتراك. لا ترسل كلمة السر أو token للفريق أو للمحادثة.

## 5. افحص الجاهزية ثم ارفع

من جذر المشروع:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Deploy-Azure-Student.ps1 -Prefix logiccircuit26 -ResourceGroup logic-circuit-rg -Location westeurope -CheckOnly
```

هذا الفحص لا ينشئ موارد؛ يفحص الأدوات، الاشتراك المختار، وضع Docker وصحة تركيب Bicep. لا يثبت سماح السياسات أو الحصص لكل مورد. إذا نجح، نفّذ:

```powershell
.\scripts\Deploy-Azure-Student.ps1 -Prefix logiccircuit26 -ResourceGroup logic-circuit-rg -Location westeurope
```

سيطلب كلمة مرور مسؤول قاعدة البيانات بشكل مخفي. استعمل 12 حرفاً أو أكثر مع حروف كبيرة وصغيرة وأرقام ورمز، بدون فاصلة منقوطة أو علامة تنصيص مزدوجة أو سطر جديد. احفظها عندك للتحديثات، ولا تضعها في الكود. تطبق Azure سياسة كلمة مرورها أيضاً.

السكربت يبني صورة Linux على جهازك أولاً، ويشغّل اختبار AND باستخدام `read_slang` ضمن Dockerfile. إذا فشل البناء يتوقف قبل طلب إنشاء موارد جديدة. بعد نجاحه ينشئ الموارد، ويرفع الصورة إلى ACR باستخدام `docker push`، ويعيد تشغيل التطبيق وينتظر صحة قاعدة البيانات. يستخدم Web App هوية Managed identity لسحب الصورة. يحتاج حساب النشر صلاحية إنشاء الموارد وتعيين AcrPull ودفع الصور؛ اشتراك Contributor وحده قد لا يسمح بتعيين الأدوار.

استخدم هذا السكربت الجديد لحساب الطالب. السكربت القديم `Deploy-Azure.ps1` محفوظ لكنه يعتمد على `az acr build`؛ تشير وثائق Microsoft حالياً إلى توقف ACR Tasks الممولة بالرصيد المجاني مؤقتاً. الطريقة الجديدة لا تستخدم ACR Tasks.

أول تشغيل لـWeb App قد يتأخر أثناء تنزيل الصورة الكبيرة. يعرض السكربت `Application ready: https://...azurewebsites.net` بعد نجاح `/api/health/database`. نجاح الموارد وحده لا يكفي. إذا فشل النشر في المنتصف فقد تبقى موارد مدفوعة؛ راجعها قبل إعادة المحاولة.

## 6. اختبر الرابط قبل GitHub

افتح الرابط الذي طُبع، وليس localhost. جرّب:

| التجربة | النتيجة المطلوبة |
| --- | --- |
| `/api/health` و`/api/health/database` | `status` يساوي `ok` |
| استيراد `examples/full_adder.sv` | Top module هو `full_adder`، مع 10 مكونات و12 وصلة |
| Truth Table | 8 صفوف، Sum وCarry صحيحان |
| Save ثم تحديث الصفحة ثم Refresh saved circuits ثم Load | ترجع الدارة نفسها من Azure SQL |
| إعادة تشغيل Web App ثم Load | تبقى البيانات وملكية المتصفح |
| Community ثم Open copy من متصفح آخر | نسخة قابلة للتعديل مستقلة عن المنشور |
| نشر ثم Unpublish من المتصفح المالك | يختفي المنشور |
| هاتف حقيقي عبر بيانات الهاتف | الرابط يفتح، والتوصيل والتحريك والسلايدرز ورفع SV تعمل |
| تدوير الهاتف | اللوحات وجدول الحقيقة يبقيان قابلين للاستخدام |

للفحوص المرفقة:

```powershell
.\scripts\Run-Checks.ps1 -ApiBaseUrl "https://YOUR-APP.azurewebsites.net/"
dotnet run --project tests/PresentationChecks
```

استبدل الرابط بعنوان تطبيقك الحقيقي. فحص API ينشئ ويحذف سجلات اختبار ويشغّل Yosys، فاستعمل نشر التجربة قبل إدخال بيانات مهمة. نجاح health لقاعدة البيانات لا يثبت تشغيل Yosys؛ اختبار SV هو الذي يتحقق منه. صفحة المجتمع عامة، والحفظ الخاص يعتمد على Cookie للمتصفح، وليس حساب مستخدم مسجلاً.

## 7. لو حدث خطأ

من Azure Portal افتح Resource group ثم Web App ثم Log stream. أو استخدم الاسم الذي طبعه السكربت:

```powershell
az webapp log tail --name YOUR-APP-NAME --resource-group logic-circuit-rg
```

| الخطأ | ما تراجعه |
| --- | --- |
| Docker لا يعمل | افتح Docker Desktop وتأكد من Linux containers. |
| Policy أو Region | راجع نص الخطأ والمنطقة المسموحة في الاشتراك. لا تعاود إنشاء مجموعات عشوائياً. |
| AuthorizationFailed عند roleAssignments | حسابك يحتاج صلاحية تعيين دور AcrPull على الريجستري. |
| فشل تنزيل الصورة | راجع نجاح docker push وهوية التطبيق ودور AcrPull؛ قد يحتاج الدور وقتاً لينتشر. |
| خطأ 503 أو توقف البدء | راجع السجل، اتصال Azure SQL والترحيلات، ومساحة الذاكرة في الخطة. |
| Module demo not found | اكتب اسم الموديول المعلن، مثل full_adder. |
| Unsupported cell | النسخة تدعم البوابات الست أحادية البت، ولا تدعم flip-flops أو buses. |
| الدوائر الخاصة غير ظاهرة بعد تغيير المتصفح | الملكية Cookie للمتصفح؛ افتح المتصفح نفسه أو نسخة JSON احتياطية. |

لا تغيّر `ApiBaseUrl` إلى localhost في نسخة السيرفر. العميل والـAPI مستضافان معاً ويستخدمان الأصل نفسه. لا ترفع مجلد أدوات Windows ولا قواعد البيانات المحلية ولا .env ولا كلمات المرور.

## 8. التحديث بعد نجاح التجربة

نزّل نسخة JSON لدوائرك المهمة أولاً. أعد تشغيل سكربت الطالب بنفس Prefix وResourceGroup والمنطقة وبيانات مسؤول SQL. يبني الصورة المعدلة ويرفعها ثم يعيد التشغيل. اترك قاعدة CircuitDb ومجلد `/home/data-protection-keys` محفوظين حتى تستمر البيانات وملكية المتصفح. خزّن هذه القيم لديك؛ لا تبدّلها عشوائياً في تحديث عادي.

بعد نجاح التجربة يمكنك رفع الكود على GitHub. رفع GitHub وحفظ الكود لا ينشر التطبيق وحده. لحذف تجربة Azure نهائياً استخدم Resource group في البوابة بعد أخذ نسخ احتياطية والتأكد من أنها تخص هذا المشروع فقط؛ حذفها يحذف التطبيق وقاعدة بياناته.

## حالة المراجعة وحدودها

فُحص البناء المحلي، والمنطق، والتفاعل في المتصفح، والحفظ بملف SQLite. جرى فحص تركيب Bicep وPowerShell وتسلسل سكربت النشر بردود محاكاة، ولم ينشئ ذلك موارد Azure. لا تتضمن هذه الحزمة ادعاء نجاح بناء Docker فعلي أو Azure SQL فعلي أو اختبار هاتف مادي. هذه الخطوات تنفذها من حسابك ثم نراجع أي خطأ يظهر.

## المصادر الرسمية

- https://learn.microsoft.com/azure/container-registry/container-registry-tasks-overview
- https://learn.microsoft.com/azure/app-service/configure-custom-container
- https://learn.microsoft.com/cli/azure/install-azure-cli-windows
- https://docs.docker.com/desktop/setup/install/windows-install/
- https://learn.microsoft.com/azure/role-based-access-control/role-assignments-template
- https://learn.microsoft.com/azure/cost-management-billing/manage/spending-limit
