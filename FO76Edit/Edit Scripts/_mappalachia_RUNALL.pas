// Run every Mappalachia script consecutively
unit Mappalachia;

uses
	_mappalachia_entity,
	_mappalachia_scrap,
	_mappalachia_location,
	_mappalachia_position,
	_mappalachia_region,
	_mappalachia_space;

	function initialize: Integer;
	var
		i : Integer;
	begin
		for i := 0 to FileCount() -1 do begin
			esmNumber := i;
			targetESM := FileByIndex(esmNumber);
			fileName := GetFileName(targetESM);

			if (pos('.esm', fileName) = 0) then begin
				AddMessage('Skipping ' + fileName + ' - not an ESM');
				continue
			end;

			AddMessage('Now running _mappalachia_space (' + fileName + ')...');
			_mappalachia_space.initialize();

			AddMessage('Now running _mappalachia_location (' + fileName + ')...');
			_mappalachia_location.initialize();

			AddMessage('Now running _mappalachia_region (' + fileName + ')...');
			_mappalachia_region.initialize();

			AddMessage('Now running _mappalachia_scrap (' + fileName + ')...');
			_mappalachia_scrap.initialize();

			AddMessage('Now running _mappalachia_entity (' + fileName + ')...');
			_mappalachia_entity.initialize();

			AddMessage('Now running _mappalachia_position (' + fileName + ')...');
			_mappalachia_position.initialize();
		end;

		AddMessage('Mappalachia export finished.');
	end;
end.
