# Select strategies

Open [Selection result]. Each row is for one site and one connection type, for example HTTPS over IPv4.

### Select an applicable strategy

1. Use the search to find the necessary site.
2. Show the successful results.
3. Select [Best results first].
4. Compare the success rate first. Then compare the response time.
5. Examine [Without Zapret2]. If the site is always available without bypass settings, it does not need a strategy.
6. Before you use a row with a warning icon, read the explanation. The strategy possibly passed only some related connection types.

The most reliable strategy passes all repeated attempts. A `5/5 (100%)` result is usually better than `1/1 (100%)`, because the test was repeated more times.

### Test the selected strategy again

- [Test with CURL] repeats the automatic requests. It updates the result of the row.
- [Manual component test] starts the selected strategy temporarily. During this test, open the site in your usual browser or application. Then stop the test.

A manual browser check is very important when the automatic request is successful but the service does not fully operate.

### Select a scope

- [Add for site] applies the strategy only to the selected site with the same connection type.
- [Add for site list] applies the strategy to the full related TXT list with the same connection type.

Use the list scope only if the strategy operates for all necessary sites in the list. If the sites need different strategies, add each strategy separately with [Add for site].

You cannot add the same strategy two times to the same scope. After you add a strategy, it shows in [Preset builder]. The preset preview updates automatically.

### For advanced users

The automatic check repeats one network request. It does not do all the operations of a browser. It does not prove that additional addresses, JavaScript, video or sign-in operate. Use the request results and a manual service test before you make the final decision.

Next: [Scopes, sequence and warnings](cdpiui://Help/Zapret2ManualPresetBuilding/3OrganizeManualPreset/).
