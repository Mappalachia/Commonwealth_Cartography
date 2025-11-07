// Gets a list of all MISC and which items in which quantities(KYWD) they give when scrapped
// Header 'scrapFormID,component,componentQuantity'
unit _mappalachia_scrap;

	uses _mappalachia_lib;

	var	outputStrings : TStringList;

	procedure Initialize;
	begin
		processRecordGroup('MISC', 'Scrap');
	end;

	procedure ripItem(item : IInterface);
	const
		CVPAEntry = ElementBySignature(item, 'CVPA');
		formID = IntToStr(FixedFormId(item));
	var
		i : Integer;
		currentComponent : IInterface;
	begin
		for i:= 0 to ElementCount(CVPAEntry) - 1 do begin
			currentComponent := ElementByName(CVPAEntry, 'Component #' + IntToStr(i));
			outputStrings.Add(
				formID + ',' +
				sanitize(GetEditValue(ElementByName(currentComponent, 'Component'))) + ',' +
				sanitize(GetEditValue(ElementByName(currentComponent, 'Count')))
			);
		end;
	end;
end.
