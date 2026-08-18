
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Data;

public class EmergencyContactsController : Controller
{
    private readonly ApplicationDbContext _context;

    public EmergencyContactsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: EMERGENCYCONTACTS
    public async Task<IActionResult> Index()    
    {
        return View(await _context.EmergencyContacts.ToListAsync());
    }

    // GET: EMERGENCYCONTACTS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var emergencycontact = await _context.EmergencyContacts
            .FirstOrDefaultAsync(m => m.ID == id);
        if (emergencycontact == null)
        {
            return NotFound();
        }

        return View(emergencycontact);
    }

    // GET: EMERGENCYCONTACTS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: EMERGENCYCONTACTS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ID,PatientID,Patient,ContactName,ContactPhoneNumber,Relationship")] EmergencyContact emergencycontact)
    {
        if (ModelState.IsValid)
        {
            _context.Add(emergencycontact);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(emergencycontact);
    }

    // GET: EMERGENCYCONTACTS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var emergencycontact = await _context.EmergencyContacts.FindAsync(id);
        if (emergencycontact == null)
        {
            return NotFound();
        }
        return View(emergencycontact);
    }

    // POST: EMERGENCYCONTACTS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("ID,PatientID,Patient,ContactName,ContactPhoneNumber,Relationship")] EmergencyContact emergencycontact)
    {
        if (id != emergencycontact.ID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(emergencycontact);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmergencyContactExists(emergencycontact.ID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(emergencycontact);
    }

    // GET: EMERGENCYCONTACTS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var emergencycontact = await _context.EmergencyContacts
            .FirstOrDefaultAsync(m => m.ID == id);
        if (emergencycontact == null)
        {
            return NotFound();
        }

        return View(emergencycontact);
    }

    // POST: EMERGENCYCONTACTS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var emergencycontact = await _context.EmergencyContacts.FindAsync(id);
        if (emergencycontact != null)
        {
            _context.EmergencyContacts.Remove(emergencycontact);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool EmergencyContactExists(int? id)
    {
        return _context.EmergencyContacts.Any(e => e.ID == id);
    }
}
